using System.Security.Cryptography;
using System.Text;
using Entities.Game;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using PickleVerse.Services.Utilities;

static class ClientSecurityChecks
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
        var settingsPath = Path.Combine(TestSettings.Root, "My project", "Assets", "Resources", "BackendApiSettings.asset");
        var baseUrl = File.ReadLines(settingsPath).Single(line => line.StartsWith("  baseUrl: "))["  baseUrl: ".Length..];
        Check(ApiUrl.Build(baseUrl, "api/Auth/login", development: false) == "https://tad-min.io.vn/api/Auth/login",
            "the packaged Unity API settings can reach the HTTPS login endpoint in release builds");
        string secret = new('a', 64), match = Guid.NewGuid().ToString("N"), lease = Guid.NewGuid().ToString("N"), player = Guid.NewGuid().ToString("N");
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] Token(long expires, string matchId, string playerId)
        {
            return DedicatedToken.Create(secret, matchId, playerId, lease, Guid.NewGuid().ToString("N"), expires, 7);
        }
        var valid = Token(now + 120, match, player);
        Check(valid.Length == 109 && valid.Length <= 128, "signed ticket fits Fusion 2.1.3 transport limit");
        Check(Convert.FromBase64String(Convert.ToBase64String(valid)).SequenceEqual(valid), "HTTP base64 preserves all connection token bytes");
        Check(DedicatedAdmission.Verify(valid, secret, match, lease, now)?.playerId == player, "backend signature accepted by actual Unity admission verifier");
        Check(DedicatedAdmission.Verify(valid, secret, match, lease, now)?.authVersion == 7, "login epoch is signed and preserved by the Unity verifier");
        var changedVersion = (byte[])valid.Clone(); changedVersion[76] ^= 1;
        Check(DedicatedAdmission.Verify(changedVersion, secret, match, lease, now) == null, "login epoch cannot be forged");
        Check(DedicatedAdmission.ConnectionIdentity(player, 7) != DedicatedAdmission.ConnectionIdentity(player, 8), "new logins use distinct Photon identities during takeover");
        var sessions = new DedicatedPlayerSessions();
        Check(sessions.TryAdmit(player, 7, 1, out var previous) && previous == null, "first authenticated peer is admitted");
        Check(!sessions.TryAdmit(player, 7, 2, out _) && !sessions.TryAdmit(player, 6, 2, out _), "duplicate and stale connected sessions are rejected");
        Check(sessions.TryAdmit(player, 8, 2, out previous) && previous.Peer == 1, "new login takes over exactly the previous peer");
        sessions.RemovePeer(1);
        Check(sessions.Find(player)?.Peer == 2, "late disconnect from the old device preserves the new connection");
        Check(sessions.TakeExpired(player, 7) == null && sessions.Find(player)?.Peer == 2, "late heartbeat response cannot disconnect the replacement");
        Check(sessions.TakeExpired(player, 8)?.Peer == 2 && sessions.Find(player) == null, "revoked epoch disconnects its current peer");
        Check(sessions.TryAdmit(player, 8, 3, out _), "ordinary reconnect can reuse the current login after disconnect");
        Check(DedicatedAdmission.Verify(valid, new string('b', 64), match, lease, now) == null, "wrong service key rejected");
        Check(DedicatedAdmission.Verify(valid, secret, Guid.NewGuid().ToString("N"), lease, now) == null, "ticket cannot cross matches");
        Check(DedicatedAdmission.Verify(valid, secret, match, "different-lease", now) == null, "ticket cannot cross allocations");
        Check(DedicatedAdmission.Verify(Token(now, match, player), secret, match, lease, now) == null, "expired ticket rejected");
        Check(DedicatedAdmission.Verify(Token(now + 9999, match, player), secret, match, lease, now) == null, "unbounded ticket lifetime rejected");
        Check(DedicatedAdmission.Verify(Token(now + 120, match, Guid.Empty.ToString("N")), secret, match, lease, now) == null, "invalid player identity rejected");
        var tampered = (byte[])valid.Clone(); tampered[20] ^= 1;
        Check(DedicatedAdmission.Verify(tampered, secret, match, lease, now) == null, "signed player identity cannot be modified");
        Check(DedicatedAdmission.Verify(valid[..^1], secret, match, lease, now) == null, "truncated transport ticket rejected");
        Check(DedicatedAdmission.Verify(Encoding.UTF8.GetBytes("invalid.token"), secret, match, lease, now) == null, "malformed ticket rejected");
        Check(DedicatedAdmission.Verify(new byte[5000], secret, match, lease, now) == null, "oversized ticket rejected");
        Check(QuickCoachingRules.Tip(null) == "", "missing telemetry never produces a finding");
        var assisted = new ShotTelemetry { outcome = "LateServe", timingError = .3f, assisted = true };
        Check(QuickCoachingRules.Tip([assisted]) == "", "assisted timing excluded");
        assisted.assisted = false;
        Check(QuickCoachingRules.Tip([assisted]).Contains("trễ"), "observable unassisted late serve gets a tip");
        Check(QuickCoachingRules.Tip([new() { kitchen = true, volley = false }]) == "", "standing in kitchen alone is not a volley fault");
        Check(QuickCoachingRules.Tip([new() { outcome = "Net" }], 1) == "", "previous rally excluded from current feedback");
        Console.WriteLine($"SUCCESS: {checks} client URL, admission and coaching assertions against actual Unity source.");
    }
}
