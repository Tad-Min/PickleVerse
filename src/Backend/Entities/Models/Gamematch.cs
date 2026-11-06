using Entities.Enums;
namespace Entities.Models;

public partial class Gamematch
{
    public Guid Matchid { get; set; }
    public string Kind { get; set; } = "";
    public MatchStatus Status { get; set; } = MatchStatus.Preparing;
    public MatchAuthorityMode Authoritymode { get; set; } = MatchAuthorityMode.Shared;
    public DateTime? Serverheartbeatat { get; set; }
    public string Ratingpolicyversion { get; set; } = "legacy-v1";
    public short Mode { get; set; } = 1;
    public string? Photonsession { get; set; }
    public Guid? Searchid { get; set; }
    public string? Region { get; set; }
    public bool Night { get; set; }
    public short Venue { get; set; }
    public DateTime Createdat { get; set; }
    public DateTime Updatedat { get; set; }
    public DateTime? Startedat { get; set; }
    public DateTime? Endedat { get; set; }
    public long? Sequence { get; set; }
    public virtual Matchhistoryreplay? Matchhistoryreplay { get; set; }
    public virtual ICollection<Matchscore> Matchscores { get; set; } = new List<Matchscore>();
    public virtual ICollection<Ratingledger> Ratingledgers { get; set; } = new List<Ratingledger>();
    public virtual ICollection<Gamematchplayer> Gamematchplayers { get; set; } = new List<Gamematchplayer>();
    public virtual ICollection<Playertechniqueserror> Playertechniqueserrors { get; set; } = new List<Playertechniqueserror>();
    public virtual ICollection<Reportplayerinmatch> Reportplayerinmatches { get; set; } = new List<Reportplayerinmatch>();
}
