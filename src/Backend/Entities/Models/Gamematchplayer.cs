namespace Entities.Models;

public partial class Gamematchplayer
{
    public Guid Matchid { get; set; }
    public Guid Playerid { get; set; }
    public short Team { get; set; }
    public int Elobefore { get; set; } = 100;
    public string? Groupid { get; set; }
    public short Rosterindex { get; set; }
    public bool Isreplaywriter { get; set; }
    public int Validreturns { get; set; }
    public int Validserves { get; set; }
    public string Endreason { get; set; } = "";
    public DateTime? Reportedat { get; set; }
    public DateTime Lastseenat { get; set; }
    public string? Outcome { get; set; }
    public bool Localforfeit { get; set; }
    public DateTime? Finalizedat { get; set; }
    public virtual Gamematch Match { get; set; } = null!;
    public virtual Playerprofile Player { get; set; } = null!;
}
