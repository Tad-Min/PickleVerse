using System;

namespace PickleVerse.Rules
{
    public enum Fault { None, ServeFoot, ServeMotion, ServeTimeout, ServeShort, ServeWrongCourt, Out, OwnCourt, TwoBounce, DoubleBounce, DoubleHit, KitchenVolley, KitchenMomentum, NetTouch, BodyHit, NetPlane, PermanentObject, ServeMiss }
    public struct CourtPoint
    {
        public float x, y, z;
        public CourtPoint(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public static class Court
    {
        // Measurements are to the outside edges of the lines, in metres.
        public const float HalfWidth = 3.048f, HalfLength = 6.7056f, Kitchen = 2.1336f;
        public const float LineWidth = .0508f, BallRadius = .037f;
        // 22 feet between the inside faces of the net posts.
        public const float NetHalfSpan = 3.3528f;
        public static bool InBounds(CourtPoint p) => Math.Abs(p.x) <= HalfWidth && Math.Abs(p.z) <= HalfLength;
        public static bool InKitchen(CourtPoint foot, float soleRadius = .09f) => Math.Abs(foot.x) <= HalfWidth + soleRadius && Math.Abs(foot.z) <= Kitchen + soleRadius;
        public static bool LegalServeFeet(CourtPoint left, CourtPoint right, int end, int score)
        {
            int lane = (score % 2 == 0 ? 1 : -1) * -end;
            return ServeFoot(left, end, lane) && ServeFoot(right, end, lane);
        }
        static bool ServeFoot(CourtPoint foot, int end, int lane) => foot.z * end > HalfLength + .09f && foot.x * lane > .09f && foot.x * lane < HalfWidth - .09f;
        public static float NetHeight(float x) => .8636f + .0508f * Math.Min(1f, Math.Abs(x) / HalfWidth);
    }
    // Pure C#: no Unity state, backend, wall clock, or input device dependencies.
    public sealed class SinglesRules
    {
        public int Server { get; private set; }
        public int LastHitter { get; private set; } = -1;
        public int ExpectedHitter => LastHitter < 0 ? Server : 1 - LastHitter;
        public int Stroke { get; private set; }
        public int Bounces { get; private set; }
        public bool Live { get; private set; }
        public Fault LastFault { get; private set; }
        public int FaultPlayer { get; private set; } = -1;
        public readonly int[] Points = new int[2], Games = new int[2], Timeouts = { 2, 2 };
        public int GameNumber => Games[0] + Games[1] + (GameFinished ? 0 : 1);
        public int Winner { get; private set; } = -1;
        public bool GameFinished { get; private set; }
        public bool EndChangeDue { get; private set; }
        bool decidingEndsChanged;
        int gameFirstServer;
        int serverEnd;

        public SinglesRules(int firstServer = 0) { Server = firstServer; gameFirstServer = firstServer; }
        public void PrepareServe(int end)
        {
            serverEnd = end; LastHitter = -1; Stroke = Bounces = 0; Live = false;
            LastFault = Fault.None; FaultPlayer = -1; EndChangeDue = false;
        }
        public Fault Serve(int player, CourtPoint left, CourtPoint right, CourtPoint contact, float elapsed, bool upwards = true, bool headBelowWrist = true)
        {
            if (player != Server || Stroke != 0 || LastFault != Fault.None) return Fault.None;
            if (elapsed > 10f) return Fail(player, Fault.ServeTimeout);
            if (!Court.LegalServeFeet(left, right, serverEnd, Points[Server])) return Fail(player, Fault.ServeFoot);
            if (contact.y > 1.05f || contact.y < .15f || !upwards || !headBelowWrist) return Fail(player, Fault.ServeMotion);
            Live = true; LastHitter = player; Stroke = 1; Bounces = 0; return Fault.None;
        }
        public Fault Hit(int player, CourtPoint contact, bool feetInKitchen)
        {
            if (!Live) return Fault.None;
            if (player == LastHitter) return Fail(player, Fault.DoubleHit);
            int end = player == Server ? serverEnd : -serverEnd;
            if (contact.z * end <= 0f) return Fail(player, Fault.NetPlane);
            if (Bounces == 0 && Stroke <= 2) return Fail(player, Fault.TwoBounce);
            if (Bounces == 0 && feetInKitchen) return Fail(player, Fault.KitchenVolley);
            LastHitter = player; Stroke++; Bounces = 0; return Fault.None;
        }
        public Fault Bounce(CourtPoint p)
        {
            if (!Live) return Fault.None;
            if (Bounces > 0) return Fail(ExpectedHitter, Fault.DoubleBounce);
            if (!Court.InBounds(p)) return Fail(LastHitter, Fault.Out);
            int hitterEnd = LastHitter == Server ? serverEnd : -serverEnd;
            if (p.z * hitterEnd >= 0) return Fail(LastHitter, Fault.OwnCourt);
            if (Stroke == 1)
            {
                if (Math.Abs(p.z) <= Court.Kitchen) return Fail(Server, Fault.ServeShort);
                int serverLane = (Points[Server] % 2 == 0 ? 1 : -1) * -serverEnd;
                // The centre line belongs to the service court. Kitchen line does not.
                if (p.x * serverLane > Court.LineWidth / 2f) return Fail(Server, Fault.ServeWrongCourt);
            }
            Bounces = 1; return Fault.None;
        }
        public Fault Fail(int player, Fault fault, bool allowDeadMomentum = false)
        {
            if (Winner >= 0) return Fault.None;
            if (LastFault != Fault.None && !allowDeadMomentum) return LastFault;
            Live = false; FaultPlayer = player; LastFault = fault; return fault;
        }
        // Delay scoring until any volley momentum is settled, including after dead ball.
        public void CommitRally()
        {
            if (FaultPlayer < 0 || LastFault == Fault.None || GameFinished || Winner >= 0) return;
            int rallyWinner = 1 - FaultPlayer;
            if (rallyWinner == Server) Points[Server]++;
            else Server = rallyWinner;
            if (Points[rallyWinner] >= 11 && Points[rallyWinner] - Points[1 - rallyWinner] >= 2)
            {
                Games[rallyWinner]++; GameFinished = true;
                if (Games[rallyWinner] == 2) Winner = rallyWinner;
            }
            if (!GameFinished && GameNumber == 3 && !decidingEndsChanged && Math.Max(Points[0], Points[1]) >= 6)
            { EndChangeDue = true; decidingEndsChanged = true; }
            FaultPlayer = -1;
        }
        public void NextGame()
        {
            if (!GameFinished || Winner >= 0) return;
            Points[0] = Points[1] = 0; Timeouts[0] = Timeouts[1] = 2;
            gameFirstServer = 1 - gameFirstServer; Server = gameFirstServer; GameFinished = false;
            PrepareServe(serverEnd);
        }
        public bool TakeTimeout(int player)
        {
            if (Live || Stroke != 0 || Timeouts[player] <= 0 || Winner >= 0) return false;
            Timeouts[player]--; return true;
        }
    }
}
