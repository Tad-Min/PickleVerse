using System.Net;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Repositories.Context;
using Services.Configuration;
using Services.Implementations;

static class RankedDoublesChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check)
    {
        const int waitSeconds = 2;
        ServerSettings Settings(bool dedicated) => new(new ConfigurationBuilder().AddConfiguration(TestSettings.Configuration())
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Matchmaking:BotFallbackSeconds"] = waitSeconds.ToString(),
                ["DedicatedServer:Enabled"] = dedicated ? "true" : "false",
                ["DedicatedServer:ServiceKey"] = "ranked-doubles-tests-only-1234567890123456",
                ["HybridAi:RatingPolicyVersion"] = EloPolicy.CurrentVersion
            }).Build());
        var dedicated = Settings(true);
        var shared = Settings(false);
        var leases = new DedicatedLeaseRepository(TestRedis.Connection, TestRedis.Settings);
        string worker = Guid.NewGuid().ToString("N");
        async Task<T> Game<T>(Func<OnlineGameService, Task<T>> action, bool useDedicated = false)
        {
            await using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            var settings = useDedicated ? dedicated : shared;
            var replays = new MatchReplayService(unit, TestReplayStorage.Shared, NullLogger<MatchReplayService>.Instance);
            return await action(new OnlineGameService(unit, new GameCatalogService(unit, settings), clock,
                settings, TestRedis.State, TestRedis.Leaderboard, replays, leases));
        }
        async Task<Guid[]> Players(params int[] ratings)
        {
            await using var db = new PickleVerseContext(options);
            var role = await db.Roles.SingleAsync(r => r.Rolename == "Player");
            var ids = ratings.Select(_ => Guid.NewGuid()).ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                db.Users.Add(new User { Userid = id, Email = id + "@doubles.invalid", Passwordhash = "disabled", Roleid = role.Roleid, Status = UserStatus.Active });
                db.Playerprofiles.Add(new Playerprofile { Userid = id, Playername = "Dbl" + id.ToString("N")[..10], Elo = ratings[i] });
            }
            await db.SaveChangesAsync();
            return ids;
        }
        static string Region() => "dbl-" + Guid.NewGuid().ToString("N")[..12];
        static RankedSearchRequest Search(string region, Guid[]? party = null, int protocol = RankedSearchRequest.RankedDoublesProtocol) => new()
        {
            SearchId = Guid.NewGuid(), Mode = PlayMode.Doubles, Region = region, Protocol = protocol,
            Members = party ?? [], Group = party == null ? "" : party[0].ToString("N")
        };
        Task<RankedSearchTicket> Begin(Guid player, RankedSearchRequest search) => Game(g => g.BeginRankedSearch(player, search));
        Task<RankedSearchTicket> Poll(Guid player, RankedSearchRequest search) => Game(g => g.PollRankedSearch(player, search));
        static OnlineMatchRequest Request(RankedSearchTicket ticket, RankedSearchRequest search) => new()
        {
            Roster = GameJson.Read<MatchRoster>(GameJson.Write(ticket.roster)),
            SearchId = search.SearchId.ToString("N"), HostPlayerId = ticket.hostPlayerId, Session = ticket.session
        };
        static DedicatedPlayerResult Result(MatchRoster roster, RoomMember member)
        {
            bool won = member.team == 0;
            return new()
            {
                onlineId = member.id,
                result = new MatchResult
                {
                    id = roster.matchId, playerId = member.id, kind = MatchKind.MatchedDoubles,
                    outcome = won ? MatchOutcome.Won : MatchOutcome.Lost,
                    gamesWon = won ? 2 : 0, gamesLost = won ? 0 : 2,
                    scores = won ? [new(11, 4, true), new(11, 6, true)] : [new(4, 11, true), new(6, 11, true)],
                    points = won ? 22 : 10
                }
            };
        }
        async Task Start(RankedSearchTicket ticket, Dictionary<Guid, RankedSearchRequest> searches, bool useDedicated = false)
        {
            foreach (var (player, search) in searches)
            {
                var accepted = await Game(g => g.PrepareOnline(player, Request(ticket, search)), useDedicated);
                if (accepted.phase != "PREPARING")
                    throw new InvalidOperationException("The fixture did not reach confirmation.");
            }
            var host = Guid.Parse(ticket.hostPlayerId);
            await Game(g => g.StartOnline(host, Request(ticket, searches[host])), useDedicated);
        }

        var solo = (await Players(400))[0];
        var soloSearch = Search(Region());
        check((await Begin(solo, soloSearch)).roster == null, "solo doubles waits for humans before using bots");
        clock.Now = clock.Now.AddSeconds(waitSeconds);
        var concurrent = await Task.WhenAll(Poll(solo, soloSearch), Poll(solo, soloSearch));
        var soloTicket = concurrent[0];
        check(concurrent.All(t => t.state == MatchmakingSearchStatus.Matched && t.matchId == soloTicket.matchId)
            && soloTicket.roster is { Count: 4, HumanCount: 1 } && soloTicket.roster.IsValid()
            && soloTicket.roster.members.Where(m => m.bot).All(m => m.elo == 520)
            && soloTicket.roster.members.Count(m => m.bot && m.team == soloTicket.roster.Find(solo.ToString("N")).team) == 1,
            "at the deadline concurrent polls assign one doubles roster with a bot teammate and two bot opponents");
        check((await Game(g => g.PublishRankedRoom(solo, soloSearch))).roomReady,
            "a filled doubles roster can publish the normal Photon room");
        var tampered = Request(soloTicket, soloSearch);
        tampered.Roster.members.First(m => m.bot).elo = 9999;
        var waiting = await Game(g => g.OfferOnline(solo, tampered), true);
        check(waiting.phase == "WAITING_CAPACITY" && waiting.readyRemainingSeconds == 0
            && waiting.roster.members.Where(m => m.bot).All(m => m.elo == 520),
            "the API rejects client-supplied bot ratings and waits for a worker before confirming");
        await leases.Poll(worker, dedicated.Dedicated.LeaseSeconds);
        var offer = await Game(g => g.OfferOnline(solo, Request(soloTicket, soloSearch)), true);
        check(offer.phase == "PREPARING" && !offer.allAccepted && offer.roster.members.Count(m => m.ready) == 3,
            "the confirmation counts bot seats and waits for the one human");
        await Start(soloTicket, new() { [solo] = soloSearch }, true);
        var pending = (await Game(g => g.Read(solo), true)).player.pendingMatch;
        check(pending.Ranked && pending.opponentElo == 520 && pending.ratingPolicyVersion == MatchResult.DoublesStandardRating,
            "dedicated doubles snapshots the ranked policy and average opposing rating");

        var party = await Players(400, 440);
        string partyRegion = Region();
        var partySearch = party.ToDictionary(id => id, _ => Search(partyRegion, party));
        foreach (var (id, search) in partySearch) await Begin(id, search);
        clock.Now = clock.Now.AddSeconds(waitSeconds);
        var partyTicket = await Poll(party[0], partySearch[party[0]]);
        check(partyTicket.roster is { Count: 4, HumanCount: 2 }
            && partyTicket.roster.members.Where(m => !m.bot).Select(m => m.team).Distinct().Count() == 1,
            "a two-person party remains together against two bots");
        await Start(partyTicket, partySearch);
        var partyResults = party.Select(id => Result(partyTicket.roster!, partyTicket.roster!.Find(id.ToString("N")))).ToArray();
        Task<GameSnapshot> Report(DedicatedPlayerResult r) => Game(g => g.ReportOnline(Guid.Parse(r.onlineId),
            new() { RequestId = Guid.NewGuid().ToString("N"), Result = r.result }));
        check((await Report(partyResults[0])).player.pendingMatch != null, "shared doubles still waits for the second human report");
        check((await Report(partyResults[1])).player.history.Any(r => r.id == partyTicket.matchId && r.Ranked),
            "matching shared reports complete the party's rated match");

        string partialRegion = Region();
        var partial = await Players(400, 400, 400);
        var partialSearch = partial.ToDictionary(id => id, _ => Search(partialRegion));
        foreach (var (id, search) in partialSearch) await Begin(id, search);
        clock.Now = clock.Now.AddSeconds(waitSeconds);
        var partialTicket = await Poll(partial[0], partialSearch[partial[0]]);
        check(partialTicket.roster is { Count: 4, HumanCount: 3 }, "three available humans take priority and only the remaining seat becomes a bot");
        await Game(g => g.CancelRankedSearch(partial[1], partialSearch[partial[1]]));
        check(await TestRedis.State.Active(partial[0]) == null && await TestRedis.State.Active(partial[2]) == null,
            "cancelling a filled roster releases every human's search");

        string fullRegion = Region();
        var full = await Players(400, 475, 525, 600);
        var fullSearches = full.ToDictionary(id => id, _ => Search(fullRegion));
        foreach (var id in full.Take(3)) await Begin(id, fullSearches[id]);
        clock.Now = clock.Now.AddSeconds(waitSeconds);
        var fullTicket = await Begin(full[3], fullSearches[full[3]]);
        check(fullTicket.roster is { Count: 4, HumanCount: 4 }, "an available fourth human still wins priority at the fallback deadline");
        await Start(fullTicket, fullSearches);
        foreach (var member in fullTicket.roster!.members) await Report(Result(fullTicket.roster, member));

        foreach (bool twoParties in new[] { false, true })
        {
            string region = Region();
            var players = await Players(400, 405, 415, 420);
            var searches = players.Select((id, index) => (id, search: Search(region,
                twoParties ? players.Skip(index / 2 * 2).Take(2).ToArray() : null))).ToArray();
            await Task.WhenAll(searches.Select(x => Begin(x.id, x.search)));
            var tickets = await Task.WhenAll(searches.Select(x => Poll(x.id, x.search)));
            var roster = tickets[0].roster;
            bool keepTeams = !twoParties || roster != null && players.Chunk(2)
                .All(pair => roster.Find(pair[0].ToString("N")).team == roster.Find(pair[1].ToString("N")).team);
            check(roster is { Count: 4, HumanCount: 4 } && roster.IsValid() &&
                tickets.All(t => t.state == MatchmakingSearchStatus.Matched && t.matchId == tickets[0].matchId) && keepTeams,
                twoParties ? "two simultaneously queued parties are joined immediately and kept together" :
                    "four simultaneously queued solo players get the same match before the bot deadline");
            await Game(g => g.CancelRankedSearch(searches[0].id, searches[0].search));
        }

        var active = new List<RankedSearchTicket> { soloTicket };
        for (int i = 0; i < 3; i++)
        {
            Guid player = (await Players(400))[0];
            var search = Search(Region());
            await Begin(player, search);
            clock.Now = clock.Now.AddSeconds(waitSeconds);
            var ticket = await Poll(player, search);
            await Start(ticket, new() { [player] = search });
            active.Add(ticket);
        }
        var matchIds = active.Select(t => Guid.Parse(t.matchId)).ToArray();
        await using (var db = new PickleVerseContext(options))
        {
            check(await db.Gamematches.CountAsync(m => matchIds.Contains(m.Matchid) && m.Status == MatchStatus.Playing) == 4
                && active.SelectMany(t => t.roster!.members).Count(m => m.bot) == 12,
                "four playing doubles matches reuse bot identities independently beyond the ten seeded profiles");
            using var unit = new UnitOfWork(db);
            var botIds = RankedBotAccounts.Ids.Select(Guid.Parse).ToArray();
            var activeBot = Guid.Parse(soloTicket.roster!.members.First(m => m.bot).id);
            check((await unit.Game.AvailableBots(botIds, clock.Now.UtcDateTime)).Count == botIds.Length
                && await unit.Game.PendingMatch(activeBot) == null,
                "2v2 does not reserve or block the shared bot profiles needed by singles");
        }
        var sharedSolo = active[1];
        var sharedHuman = sharedSolo.roster!.members.Single(member => !member.bot);
        await Report(Result(sharedSolo.roster, sharedHuman));
        var sharedId = Guid.Parse(sharedSolo.matchId);
        await using (var db = new PickleVerseContext(options))
        {
            var participants = await db.Gamematchplayers.Where(p => p.Matchid == sharedId).ToListAsync();
            check(participants.Count == 4 && participants.All(p => p.Finalizedat != null && p.Outcome == (p.Team == 0 ? "WIN" : "LOSE")),
                "the shared result gives the bot teammate and the opponents their respective team outcomes");
        }

        var official = soloTicket.roster!.members.Select(member => Result(soloTicket.roster, member)).ToList();
        async Task<bool> Complete() => await Game(async g =>
        {
            await g.CompleteDedicated(Guid.Parse(soloTicket.matchId), official);
            return true;
        }, true);
        await Task.WhenAll(Complete(), Complete());
        var ranked = (await Game(g => g.Read(solo), true)).player.history.Single(r => r.id == soloTicket.matchId);
        check(ranked.Ranked && ranked.eloDelta == EloPolicy.StandardDelta(400, 520, ranked.outcome == MatchOutcome.Won),
            "a doubles result uses the same Elo calculation as singles against the average opposing rating");
        var rankedId = Guid.Parse(soloTicket.matchId);
        var fullId = Guid.Parse(fullTicket.matchId);
        await using (var db = new PickleVerseContext(options))
        {
            check(await db.Ratingledgers.CountAsync(r => r.Matchid == rankedId) == 1
                && await db.Gamematchplayers.CountAsync(p => p.Matchid == rankedId && p.Finalizedat != null) == 4,
                "repeated dedicated results create one human Elo row and finalize all seats");
            check(await db.Ratingledgers.CountAsync(r => r.Matchid == fullId) == 4,
                "an all-human match from the doubles queue rates each player");
        }
        var older = (await Players(400))[0];
        var oldSearch = Search(Region(), protocol: RankedSearchRequest.RankedDoublesProtocol - 1);
        await Begin(older, oldSearch);
        clock.Now = clock.Now.AddSeconds(waitSeconds);
        check((await Poll(older, oldSearch)).roster == null
            && !new MatchResult { kind = MatchKind.MatchedDoubles, ratingPolicyVersion = EloPolicy.CurrentVersion }.Ranked,
            "older clients and historical doubles keep their original rules");
    }
}
