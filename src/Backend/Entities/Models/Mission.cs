using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Mission
{
    public int Missionid { get; set; }

    public string Missionname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int Target { get; set; }

    public string Reward { get; set; } = null!;

    public virtual ICollection<Playermission> Playermissions { get; set; } = new List<Playermission>();
}
