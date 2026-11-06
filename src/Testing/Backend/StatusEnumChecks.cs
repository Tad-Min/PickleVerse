using System.Text.Json;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

static class StatusEnumChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        void Wire<T>(params string[] expected) where T : struct, Enum
        {
            var values = Enum.GetValues<T>();
            check(values.Select(StatusWire.Format).SequenceEqual(expected), typeof(T).Name + " has explicit canonical wire codes");
            foreach (var value in values)
            {
                check(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)).Equals(value), typeof(T).Name + " JSON round-trip: " + StatusWire.Format(value));
                check(GameJson.Write(value) == JsonSerializer.Serialize(StatusWire.Format(value)), typeof(T).Name + " runtime JSON preserves canonical code: " + StatusWire.Format(value));
            }
            foreach (var invalid in new[] { "0", "999", "\"invalid\"", "null" })
            {
                bool rejected = false;
                try { JsonSerializer.Deserialize<T>(invalid); } catch (JsonException) { rejected = true; }
                check(rejected, typeof(T).Name + " rejects " + invalid);
                rejected = false;
                try { GameJson.Read<T>(invalid); } catch (JsonException) { rejected = true; }
                check(rejected, typeof(T).Name + " runtime JSON rejects " + invalid);
            }
        }
        Wire<UserStatus>("ACTIVE", "BANNED", "SUSPENDED");
        Wire<FriendRequestStatus>("PENDING", "ACCEPTED", "DECLINED");
        Wire<ReportStatus>("PENDING", "CHECKING", "ACCEPTED", "DECLINED");
        Wire<MatchStatus>("PREPARING", "PLAYING", "FINISHED", "CANCELLED");
        Wire<MatchAuthorityMode>("shared", "dedicated");
        Wire<ContentPublishStatus>("DRAFT", "PUBLISHED", "UNPUBLISHED");
        Wire<AppealStatus>("PENDING", "CHECKING", "ACCEPTED", "DECLINED");
        Wire<SyncItemStatus>("APPLIED", "DUPLICATE", "REJECTED");
        Wire<DedicatedAllocationStatus>("queued", "starting", "ready", "finishing", "pending", "busy");
        Wire<ProgressResetGroup>("LEARNING", "RANKING", "MISSIONS", "ECONOMY");
        Wire<SessionScope>("game", "appeals");
        Wire<MatchmakingSearchStatus>("SEARCHING","MATCHED","BOT","CANCELLED","TIMEOUT");

        await using var db = new PickleVerseContext(options);
        var user = await db.Users.OrderBy(u => u.Userid).FirstAsync();
        foreach (var status in Enum.GetValues<UserStatus>())
        {
            user.Status = status;
            await db.SaveChangesAsync();
            var actual = await db.Database.SqlQuery<string>($"SELECT status AS \"Value\" FROM users WHERE userid={user.Userid}").SingleAsync();
            check(actual == StatusWire.Format(status), "EF persists canonical UserStatus: " + actual);
            check(await db.Users.AsNoTracking().AnyAsync(u => u.Userid == user.Userid && u.Status == status), "EF translates typed UserStatus predicate");
        }
        user.Status = UserStatus.Active;
        var match = new Gamematch { Matchid = Guid.NewGuid(), Kind = "Custom", Status = MatchStatus.Preparing,
            Authoritymode = MatchAuthorityMode.Dedicated, Createdat = DateTime.UtcNow, Updatedat = DateTime.UtcNow };
        db.Gamematches.Add(match);
        await db.SaveChangesAsync();
        check(await db.Gamematches.AsNoTracking().AnyAsync(m => m.Matchid == match.Matchid && m.Authoritymode == MatchAuthorityMode.Dedicated
            && m.Status == MatchStatus.Preparing), "EF round-trips typed match status and authority mode");
        var raw = await db.Database.SqlQuery<string>($"SELECT authoritymode AS \"Value\" FROM gamematches WHERE matchid={match.Matchid}").SingleAsync();
        check(raw == "dedicated", "authority mode wire compatibility is preserved");
    }
}
