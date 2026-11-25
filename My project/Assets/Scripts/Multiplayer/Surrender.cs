using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Multiplayer
{
    /// <summary>Why a surrender request closed. The name travels in the message, so do not rename the values.</summary>
    public enum SurrenderEnd { None, Withdrawn, Refused, Expired, Offline, Cooldown }

    /// <summary>
    /// Server side of "Đầu hàng": one request per team. Every human of the team has to agree, so a singles player (or a
    /// doubles player whose partner is a bot) decides alone and two partners decide together. Pure logic: the match sends
    /// the messages and ends itself.
    /// </summary>
    public sealed class SurrenderDesk
    {
        /// <summary>How long both partners have to answer, and how long a team waits before it may ask again.</summary>
        public const double Seconds = 20, CooldownSeconds = 15;

        sealed class Vote
        {
            public bool open;
            public string proposer = "";
            public double closesAt, nextAllowedAt;
            public readonly HashSet<string> agreed = new HashSet<string>();
        }

        readonly Vote[] votes = { new Vote(), new Vote() };
        readonly MatchRoster roster;
        readonly Func<double> now;
        readonly Func<string, bool> online;

        /// <summary>(player, proposer, seconds left, players that agreed)</summary>
        public Action<string, string, double, string[]> Opened;
        /// <summary>(player, reason, who caused it, seconds until the team may ask again)</summary>
        public Action<string, SurrenderEnd, string, double> Closed;
        /// <summary>(team, its human players): everyone agreed, the team loses the match.</summary>
        public Action<int, string[]> Surrendered;

        public SurrenderDesk(MatchRoster roster, Func<double> now, Func<string, bool> online)
        {
            this.roster = roster;
            this.now = now;
            this.online = online;
        }

        public bool IsOpen(int team) => team is 0 or 1 && votes[team].open;

        /// <summary>The humans of the player's team; empty when the player is not a human of this match.</summary>
        public static string[] Voters(MatchRoster roster, string player)
        {
            var member = roster?.Find(player);
            return member == null || member.bot ? Array.Empty<string>()
                : roster.members.Where(m => m.team == member.team && !m.bot).Select(m => m.id).ToArray();
        }

        /// <summary>The player pressed the button: show the box to the player and to the human partner.</summary>
        public void Ask(string player)
        {
            var humans = Voters(roster, player);
            if (humans.Length == 0)
                return;
            var vote = votes[roster.Find(player).team];
            if (vote.open)
            {
                Opened?.Invoke(player, vote.proposer, vote.closesAt - now(), vote.agreed.ToArray());
                return;
            }
            string absent = humans.FirstOrDefault(id => !online(id));
            if (absent != null)
            {
                Closed?.Invoke(player, SurrenderEnd.Offline, absent, 0);
                return;
            }
            if (now() < vote.nextAllowedAt)
            {
                Closed?.Invoke(player, SurrenderEnd.Cooldown, player, vote.nextAllowedAt - now());
                return;
            }
            vote.open = true;
            vote.proposer = player;
            vote.agreed.Clear();
            vote.closesAt = now() + Seconds;
            Show(vote, humans);
        }

        public void Agree(string player)
        {
            var humans = Voters(roster, player);
            if (humans.Length == 0)
                return;
            int team = roster.Find(player).team;
            var vote = votes[team];
            if (humans.Length > 1)
            {
                // An answer that arrives after the request closed must not open a new one.
                if (!vote.open)
                    return;
                vote.agreed.Add(player);
                if (!humans.All(vote.agreed.Contains))
                {
                    Show(vote, humans);
                    return;
                }
            }
            vote.open = false;
            Surrendered?.Invoke(team, humans);
        }

        public void Refuse(string player)
        {
            var humans = Voters(roster, player);
            if (humans.Length == 0)
                return;
            var vote = votes[roster.Find(player).team];
            if (vote.open)
                Close(vote, humans, player == vote.proposer ? SurrenderEnd.Withdrawn : SurrenderEnd.Refused, player);
        }

        /// <summary>A player lost the connection: the partner can no longer answer, so the request ends.</summary>
        public void Disconnected(string player)
        {
            var humans = Voters(roster, player);
            if (humans.Length == 0)
                return;
            var vote = votes[roster.Find(player).team];
            if (vote.open)
                Close(vote, humans, SurrenderEnd.Offline, player);
        }

        public void Tick()
        {
            foreach (var vote in votes)
                if (vote.open && now() >= vote.closesAt)
                    Close(vote, Voters(roster, vote.proposer), SurrenderEnd.Expired, vote.proposer);
        }

        void Show(Vote vote, string[] humans)
        {
            var agreed = vote.agreed.ToArray();
            foreach (var id in humans)
                Opened?.Invoke(id, vote.proposer, vote.closesAt - now(), agreed);
        }

        void Close(Vote vote, string[] humans, SurrenderEnd end, string actor)
        {
            vote.open = false;
            vote.nextAllowedAt = now() + CooldownSeconds;
            foreach (var id in humans)
                Closed?.Invoke(id, end, actor, CooldownSeconds);
        }
    }

    /// <summary>What one client knows about the request of its own team.</summary>
    public sealed class SurrenderRequest
    {
        public bool Open { get; private set; }
        public string Proposer { get; private set; } = "";
        public double ClosesAt { get; private set; }
        public SurrenderEnd End { get; private set; }
        public string EndActor { get; private set; } = "";
        public double EndSeconds { get; private set; }
        /// <summary>Changes with every message of the server, so the screen can tell an old answer from a new one.</summary>
        public int Revision { get; private set; }
        string[] agreed = Array.Empty<string>();

        public bool HasAgreed(string player) => Open && Array.IndexOf(agreed, player) >= 0;

        public void Show(string proposer, double seconds, string[] agreedPlayers, double now)
        {
            Open = true;
            Proposer = proposer ?? "";
            ClosesAt = now + Math.Max(0, Math.Min(seconds, SurrenderDesk.Seconds));
            agreed = agreedPlayers ?? Array.Empty<string>();
            End = SurrenderEnd.None;
            Revision++;
        }

        public void Finish(SurrenderEnd end, string actor, double seconds)
        {
            Open = false;
            End = end;
            EndActor = actor ?? "";
            EndSeconds = seconds;
            Revision++;
        }
    }

    /// <summary>Wording of the surrender box (pure, so the screen and its tests agree).</summary>
    public static class SurrenderText
    {
        public const string Button = "Đầu hàng", LeaveButton = "Rời trận";
        public const string Accept = "ĐẦU HÀNG", KeepPlaying = "TIẾP TỤC ĐẤU", Refuse = "KHÔNG ĐẦU HÀNG", Withdraw = "HỦY";
        public const string AskTitle = "ĐẦU HÀNG?", PartnerTitle = "ĐỒNG ĐỘI MUỐN ĐẦU HÀNG", WaitTitle = "CHỜ ĐỒNG ĐỘI";
        public const string Sending = "Đang gửi tới máy chủ trận…";
        public const string NoAnswer = "Máy chủ trận chưa phản hồi. Hãy thử lại.";
        public const string LeaveTitle = "RỜI TRẬN?";
        public const string LeaveBody = "Bạn đang mất kết nối. Nếu rời trận bây giờ và không quay lại kịp, đội của bạn bị xử thua.";

        static string Cost(bool rated) => rated ? "Bạn không nhận thưởng và có thể bị trừ Elo." : "Phòng riêng không tính Elo.";

        /// <param name="partner">Name of the human partner, or null when the player decides alone.</param>
        public static string AskBody(string partner, bool team, bool rated) => partner == null
            ? $"Trận kết thúc ngay và {(team ? "đội bạn" : "bạn")} bị xử thua. {Cost(rated)}"
            : $"{partner} cũng phải xác nhận. Khi cả hai đồng ý, trận kết thúc và đội bạn bị xử thua. {Cost(rated)}";

        public static string PartnerBody(string proposer, bool rated) =>
            $"{proposer} đề nghị đầu hàng. Khi cả hai đồng ý, trận kết thúc và đội bạn bị xử thua. {Cost(rated)}";

        public static string WaitBody(string partner) => $"Bạn đã đồng ý. Đang chờ {partner} xác nhận.";

        public static string Countdown(double seconds) => $"Tự hủy sau {Math.Max(0, (int)Math.Ceiling(seconds))} giây";

        /// <summary>Line shown after a request closed, or null when the player closed it and needs no explanation.</summary>
        public static string EndNotice(SurrenderEnd end, bool byPlayer, string actor, double seconds)
        {
            switch (end)
            {
                case SurrenderEnd.Withdrawn:
                    return byPlayer ? null : $"{actor} đã rút đề nghị đầu hàng. Trận đấu tiếp tục.";
                case SurrenderEnd.Refused:
                    return byPlayer ? null : $"{actor} không đồng ý đầu hàng. Trận đấu tiếp tục.";
                case SurrenderEnd.Expired:
                    return "Hết thời gian xác nhận đầu hàng. Trận đấu tiếp tục.";
                case SurrenderEnd.Offline:
                    return byPlayer ? null : $"{actor} đang mất kết nối nên chưa thể đầu hàng.";
                case SurrenderEnd.Cooldown:
                    return $"Đội vừa đề nghị đầu hàng. Thử lại sau {Math.Max(1, (int)Math.Ceiling(seconds))} giây.";
                default:
                    return null;
            }
        }

        /// <summary>Line of the final announcement: "Huy đã đầu hàng", "Huy và An đã đầu hàng".</summary>
        public static string Reason(IReadOnlyList<string> names) =>
            (names == null || names.Count == 0 ? "Một đội" : string.Join(" và ", names)) + " đã đầu hàng";
    }
}
