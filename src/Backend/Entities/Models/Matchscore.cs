namespace Entities.Models;

public partial class Matchscore
{
    public Guid Matchid { get; set; }
    public short Gamenumber { get; set; }
    public int Teamascore { get; set; }
    public int Teambscore { get; set; }
    public bool Completed { get; set; }
    public virtual Gamematch Match { get; set; } = null!;
}
