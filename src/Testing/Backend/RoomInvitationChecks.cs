using System.Net;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;
using Repositories.Context;
using Entities.Exceptions;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class RoomInvitationChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check)
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        async Task<T> Call<T>(Func<RoomInvitationService, Task<T>> action)
        {
            await using var db = new PickleVerseContext(options);
            return await action(new RoomInvitationService(new UnitOfWork(db), new LobbyRepository(TestRedis.Connection, TestRedis.Settings), clock, TestSettings.Load()));
        }
        async Task Sql(string command, params Npgsql.NpgsqlParameter[] args)
        {
            await using var db = new PickleVerseContext(options);
            await new PostgresCommands(db).Run(command, args);
        }
        foreach (var p in new[] { (a, "Anh Minh"), (b, "Bình"), (c, "Chi") })
        {
            await Sql("INSERT INTO users(userid,email,passwordhash,roleid) SELECT @id,@email,'disabled',roleid FROM roles WHERE rolename='Player'", P("id", p.Item1), P("email", p.Item1 + "@example.invalid"));
            await Sql("INSERT INTO playerprofiles(userid,playername) VALUES(@id,@name)", P("id", p.Item1), P("name", p.Item2));
        }
        await Sql("INSERT INTO friendships(userid1,userid2) VALUES(LEAST(@a,@b),GREATEST(@a,@b))", P("a", a), P("b", b));
        var host = new LobbyPresenceRequest { SessionId = Guid.NewGuid().ToString("N"), State = "lobby", RoomCode = "INVITE12", Region = "asia", Mode = 2, IsHost = true, Members = [a.ToString("N")] };
        var guest = new LobbyPresenceRequest { SessionId = Guid.NewGuid().ToString("N"), State = "lobby" };
        async Task<LobbyPresenceReply> Host() => await Call(s => s.Publish(a, host));
        async Task<LobbyPresenceReply> Guest() => await Call(s => s.Publish(b, guest));
        async Task<RoomInvitation> Send()
        {
            // Model the real clients' recurring heartbeats during slower database checks.
            await Host();
            await Guest();
            await TestRedis.Connection.GetDatabase().KeyDeleteAsync(TestRedis.Settings.KeyPrefix + "{lobby}:cooldown:" + a.ToString("N") + ":" + b.ToString("N"));
            return await Call(s => s.Send(a, new() { PlayerId = b.ToString("N") }));
        }
        async Task Rejected(Func<Task> action, HttpStatusCode status, string name)
        {
            bool rejected = false;
            try
            {
                await action();
            }
            catch (AppException e)
            {
                rejected = e.StatusCode == status;
            }
            check(rejected, name);
        }
        await Host();
        await Guest();
        await Rejected(() => Call(s => s.Publish(c, new() { SessionId = Guid.NewGuid().ToString("N"), State = "lobby", RoomCode = host.RoomCode, Region = "asia", Mode = 2, IsHost = true, Members = [c.ToString("N")] })), HttpStatusCode.Forbidden, "another account cannot overwrite a live room registration");
        var friends = await Call(s => s.List(a, "Bình", 0));
        check(friends.Players.Length == 1 && friends.Players[0].Id == b.ToString("N") && friends.Players[0].CanInvite, "search lists only accepted friends available in the lobby with Vietnamese names");
        check((await Call(s => s.List(a, "%", 0))).Players.Length == 0, "friend search treats wildcard characters literally");
        await Rejected(() => Call(s => s.Send(a, new() { PlayerId = c.ToString("N") })), HttpStatusCode.Forbidden, "cannot invite a non-friend");
        await Host();
        await Guest();
        var sends = await Task.WhenAll(Call(s => s.Send(a, new() { PlayerId = b.ToString("N") })), Call(s => s.Send(a, new() { PlayerId = b.ToString("N") })));
        var invitation = sends[0];
        check(sends[0].Id == sends[1].Id, "concurrent duplicate sends create one pending invitation");
        check((await Guest()).Invitations.Single().SenderName == "Anh Minh", "recipient receives a named room invitation through presence polling");
        check((await Call(s => s.List(a, "", 0))).Players.Single().Pending, "sender sees the pending invitation instead of sending duplicates");
        await Rejected(() => Call(s => s.Respond(a, invitation.Id, true)), HttpStatusCode.Forbidden, "sender cannot accept the recipient's invitation");
        guest.State = "busy";
        await Host();
        await Guest();
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Conflict, "busy recipients cannot accept into another room");
        guest.State = "lobby";
        await Host();
        await Guest();
        var accepted = await Call(s => s.Respond(b, invitation.Id, true));
        check(accepted.State == "accepted" && accepted.RoomCode == host.RoomCode && accepted.Region == "asia", "accept returns the intended Photon room and region");
        check((await Guest()).Invitations.Length == 0, "accepted invitations disappear from the recipient inbox");
        check((await Call(s => s.Respond(b, invitation.Id, true))).State == "accepted", "accept can be retried without creating a second decision");
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, false)), HttpStatusCode.Gone, "a processed invitation cannot be changed to declined");
        invitation = await Send();
        await Call(s => s.Respond(b, invitation.Id, false));
        check((await Guest()).Invitations.Length == 0, "decline removes the notification");
        invitation = await Send();
        host.Members = [a.ToString("N"), c.ToString("N")];
        await Host();
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Conflict, "accept rejects a room that filled after sending");
        host.Members = [a.ToString("N")];
        await Host();
        host.RoomCode = "";
        host.IsHost = false;
        host.Members = [];
        await Host();
        check((await Guest()).Invitations.Length == 0, "closed rooms stop showing their pending invitations");
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Conflict, "cannot accept after the sender closes the room");
        host.RoomCode = "INVITE12";
        host.IsHost = true;
        host.Members = [a.ToString("N")];
        await Host();
        await Call(s => s.Respond(b, invitation.Id, false));
        invitation = await Send();
        host.SessionId = Guid.NewGuid().ToString("N");
        await Host();
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Conflict, "old invitations cannot join a replacement host session");
        await Call(s => s.Respond(b, invitation.Id, false));
        invitation = await Send();
        clock.Now = clock.Now.AddSeconds(RoomInvitationService.InvitationSeconds + 1);
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Gone, "expired invitations cannot be accepted");
        await Host();
        await Guest();
        invitation = await Send();
        clock.Now = clock.Now.AddSeconds(RoomInvitationService.PresenceSeconds + 1);
        check((await Call(s => s.List(a, "", 0))).Players.Single().Status == "offline", "presence leases expire after a client stops heartbeating");
        async Task Social(Guid self, Guid target, string action)
        {
            await using var context = new PickleVerseContext(options);
            var social = new OnlineSocialService(new UnitOfWork(context), TestRedis.Leaderboard,
                new LobbyRepository(TestRedis.Connection, TestRedis.Settings));
            await social.Act(self, new() { PlayerId = target, Action = action });
        }
        invitation = await Send();
        await Social(a, b, "block");
        check((await Guest()).Invitations.Length == 0, "blocking removes pending room invitations from the inbox");
        await Rejected(() => Call(s => s.Send(a, new() { PlayerId = b.ToString("N") })), HttpStatusCode.Forbidden, "blocker cannot send room invitations");
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Gone, "received-before-block invitation cannot be accepted");
        await Social(a, b, "unblock");
        await Sql("INSERT INTO friendships(userid1,userid2) VALUES(LEAST(@a,@b),GREATEST(@a,@b))", P("a", a), P("b", b));
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Gone, "unblock and a new friendship cannot revive an invalidated invitation");
        invitation = await Send();
        await Social(b, a, "block");
        await Rejected(() => Call(s => s.Respond(b, invitation.Id, true)), HttpStatusCode.Gone, "recipient-side block also invalidates a previously received invitation");
        var preparation = Guid.NewGuid();
        await Sql("INSERT INTO gamematches(matchid,kind,status,createdat) VALUES(@match,'Custom','PREPARING',@created)", P("match", preparation), P("created", clock.GetUtcNow().UtcDateTime));
        await Sql("INSERT INTO gamematchplayers(matchid,playerid,team) VALUES(@match,@player,0)", P("match", preparation), P("player", c));
        async Task<bool> Rename()
        {
            await using var context = new PickleVerseContext(options);
            var profiles = new ProfileService(new UnitOfWork(context), Microsoft.Extensions.Logging.Abstractions.NullLogger<ProfileService>.Instance, TestSettings.Load(), clock);
            return await profiles.UpdateProfileAsync(c, new Entities.DTOs.UpdateProfileRequest { PlayerName = "Chi mới" });
        }
        await Rejected(() => Rename(), HttpStatusCode.Conflict, "a live ready confirmation prevents profile changes");
        await Sql("UPDATE gamematches SET createdat=@created WHERE matchid=@match", P("match", preparation), P("created", clock.GetUtcNow().UtcDateTime.AddSeconds(-TestSettings.Load().Matchmaking.ReadyConfirmationSeconds - 1)));
        check(await Rename(), "an expired preparation does not permanently lock the profile");
        await Sql("UPDATE gamematches SET status='PLAYING' WHERE matchid=@match", P("match", preparation));
        await Rejected(() => Rename(), HttpStatusCode.Conflict, "an ongoing match keeps the profile locked regardless of its age");
    }
}
