using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playermatchhistory
{
    public Guid Playerid { get; set; }

    public Guid Matchid { get; set; }

    public string Status { get; set; } = null!;

    public bool Isteama { get; set; }

    public int Receivedelo { get; set; }

    public virtual Matchhistory Match { get; set; } = null!;

    public virtual Playerprofile Player { get; set; } = null!;
}
