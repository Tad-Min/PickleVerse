using PickleVerse.Progression;

namespace PickleVerse.Social
{
    public sealed class SocialPlayer
    {
        public string Id { get; }
        public string Name { get; }
        public int Elo { get; }
        public SocialPlayer(string id, string name, int elo) { Id=id; Name=name; Elo=elo; }
        public static SocialPlayer From(PlayerProfile player) => new SocialPlayer(player.id, player.playerName, player.elo);
    }

    public sealed class LeaderboardEntry
    {
        public int Position { get; }
        public SocialPlayer Player { get; }
        public LeaderboardEntry(int position, SocialPlayer player) { Position=position; Player=player; }
    }


}
