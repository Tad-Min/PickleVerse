using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playermission
{
    public Guid Playerid { get; set; }

    public int Missionid { get; set; }

    public string Periodkey { get; set; } = "";

    public int CurrentStreak { get; set; }

    public int Progress { get; set; }

    public bool Isclaimed { get; set; }

    public DateTime? Claimedat { get; set; }

    public virtual Mission Mission { get; set; } = null!;

    public virtual Playerprofile Player { get; set; } = null!;
}
