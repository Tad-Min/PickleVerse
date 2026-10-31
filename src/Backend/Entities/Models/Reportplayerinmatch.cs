using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Reportplayerinmatch
{
    public Guid Reporterid { get; set; }

    public Guid Reportedplayerid { get; set; }

    public Guid Matchid { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime Updatedat { get; set; }

    public virtual Gamematch Match { get; set; } = null!;

    public virtual Playerprofile Reportedplayer { get; set; } = null!;

    public virtual Playerprofile Reporter { get; set; } = null!;
}
