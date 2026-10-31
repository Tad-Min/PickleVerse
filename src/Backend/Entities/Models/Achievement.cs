using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Achievement
{
    public int Achievementid { get; set; }

    public string Achievementcode { get; set; } = null!;

    public string Achievementname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int Target { get; set; }

    public string Reward { get; set; } = null!;

    public virtual ICollection<Playerachievement> Playerachievements { get; set; } = new List<Playerachievement>();
}
