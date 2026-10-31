namespace Entities.Models;

// Maps the existing ratingledger table; no new SQL entity is introduced.
public partial class Ratingledger
{
    public Guid Playerid { get; set; }
    public Guid Matchid { get; set; }
    public int Beforeelo { get; set; }
    public int Afterelo { get; set; }
    public int Opponentelo { get; set; }
    public int Delta { get; set; }
    public string Policyversion { get; set; } = "";
    public DateTime Createdat { get; set; }
    public virtual Gamematch Match { get; set; } = null!;
}
