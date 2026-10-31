using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playerachievement
{
    public Guid Playerid { get; set; }

    public int Achievementid { get; set; }

    public int Progress { get; set; }

    public bool Isclaimed { get; set; }

    public DateTime? Claimedat { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;

    public virtual Playerprofile Player { get; set; } = null!;
}
