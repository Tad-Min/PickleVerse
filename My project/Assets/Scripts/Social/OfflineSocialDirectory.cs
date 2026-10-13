using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.Social
{
    // Development fixtures, deliberately independent of saved progression and online services.
    public sealed class OfflineSocialDirectory
    {
        readonly SocialPlayer[] players =
        {
            new SocialPlayer("DEMO-001", "An Bình", 7200),
            new SocialPlayer("DEMO-002", "Bảo Long", 6400),
            new SocialPlayer("DEMO-003", "Châu Anh", 5800),
            new SocialPlayer("DEMO-004", "Duy Khánh", 5200),
            new SocialPlayer("DEMO-005", "Đan Thanh", 4600),
            new SocialPlayer("DEMO-006", "Gia Huy", 4100),
            new SocialPlayer("DEMO-007", "Hà My", 3700),
            new SocialPlayer("DEMO-008", "Hoàng Nam", 3200),
            new SocialPlayer("DEMO-009", "Khánh Linh", 2800),
            new SocialPlayer("DEMO-010", "Kiên Phong", 2400),
            new SocialPlayer("DEMO-011", "Lan Chi", 2100),
            new SocialPlayer("DEMO-012", "Lê Minh", 1850),
            new SocialPlayer("DEMO-013", "Mai Anh", 1600),
            new SocialPlayer("DEMO-014", "Minh Khoa", 1400),
            new SocialPlayer("DEMO-015", "Ngọc Bích", 1250),
            new SocialPlayer("DEMO-016", "Nhật Quang", 1100),
            new SocialPlayer("DEMO-017", "Phúc An", 1000),
            new SocialPlayer("DEMO-018", "Quốc Bảo", 900),
            new SocialPlayer("DEMO-019", "Quỳnh Như", 800),
            new SocialPlayer("DEMO-020", "Sơn Tùng", 700),
            new SocialPlayer("DEMO-021", "Thanh Hà", 620),
            new SocialPlayer("DEMO-022", "Thiên Ân", 560),
            new SocialPlayer("DEMO-023", "Thùy Dương", 500),
            new SocialPlayer("DEMO-024", "Trí Dũng", 450),
            new SocialPlayer("DEMO-025", "Trúc Vy", 400),
            new SocialPlayer("DEMO-026", "Tuấn Kiệt", 320),
            new SocialPlayer("DEMO-027", "Uyên Nhi", 260),
            new SocialPlayer("DEMO-028", "Việt Anh", 220),
            new SocialPlayer("DEMO-029", "Yến Nhi", 160),
            new SocialPlayer("DEMO-030", "Vân Anh", 100)
        };
        static readonly string[] SeedFriends = { "DEMO-004", "DEMO-009", "DEMO-014", "DEMO-019", "DEMO-024", "DEMO-029" };
        readonly Dictionary<string, HashSet<string>> friends = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        HashSet<string> FriendsFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("Player ID is required.");
            if (!friends.TryGetValue(playerId, out var list))
                friends.Add(playerId, list=new HashSet<string>(SeedFriends, StringComparer.Ordinal));
            return list;
        }
        public int FriendCount(string playerId) => FriendsFor(playerId).Count;
        public bool IsFriend(string playerId, string friendId) => FriendsFor(playerId).Contains(friendId);
        public bool AddFriend(string playerId, string friendId)
        {
            if (playerId==friendId || !players.Any(p=>p.Id==friendId)) return false;
            return FriendsFor(playerId).Add(friendId);
        }
        public bool RemoveFriend(string playerId, string friendId) => FriendsFor(playerId).Remove(friendId);
        public IReadOnlyList<SocialPlayer> Search(string query) => players.Where(p=>Matches(p,query)).ToArray();
        public IReadOnlyList<SocialPlayer> GetFriends(string playerId, string query="")
        {
            var list=FriendsFor(playerId);
            return players.Where(p=>list.Contains(p.Id) && Matches(p,query)).ToArray();
        }
        public IReadOnlyList<LeaderboardEntry> Leaderboard(PlayerProfile current, string query="")
        {
            // Fixtures have hyphens in their IDs; local player IDs are strictly alphanumeric.
            // Equal Elo is ordered by ID for stable, unique positions. Filtering keeps global positions.
            return players.Concat(new[]{SocialPlayer.From(current)})
                .OrderByDescending(p=>p.Elo).ThenBy(p=>p.Id, StringComparer.Ordinal)
                .Select((p,i)=>new LeaderboardEntry(i+1,p)).Where(e=>Matches(e.Player,query)).ToArray();
        }
        static bool Matches(SocialPlayer player, string query)
        {
            string key=SearchKey(query);
            return key.Length==0 || SearchKey(player.Name).Contains(key) || SearchKey(player.Id).Contains(key);
        }
        static string SearchKey(string text)
        {
            var result=new StringBuilder();
            foreach(char c in (text??"").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark) result.Append(c=='đ'?'d':c);
            return result.ToString();
        }
    }

    public static class OfflineSocialSession
    {
        // This memory-only session never writes friends or fixture data to player JSON.
        public static OfflineSocialDirectory Directory { get; private set; } = new OfflineSocialDirectory();
        // Also resets fixtures when entering Play with domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Directory=new OfflineSocialDirectory(); }
    }
}
