using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Matchhistory
{
    public Guid Matchid { get; set; }

    public DateTime Createdat { get; set; }

    public string Matchtype { get; set; } = null!;

    public int Teamascore { get; set; }

    public int Teambscore { get; set; }

    public virtual Matchhistoryreplay? Matchhistoryreplay { get; set; }

    public virtual ICollection<Playermatchhistory> Playermatchhistories { get; set; } = new List<Playermatchhistory>();

    public virtual ICollection<Playertechniqueserror> Playertechniqueserrors { get; set; } = new List<Playertechniqueserror>();

    public virtual ICollection<Reportplayerinmatch> Reportplayerinmatches { get; set; } = new List<Reportplayerinmatch>();
}
