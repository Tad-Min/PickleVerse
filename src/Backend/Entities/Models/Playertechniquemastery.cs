using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playertechniquemastery
{
    public Guid Playerid { get; set; }

    public int Techniqueid { get; set; }

    public int Totalattempts { get; set; }

    public int Totalerrors { get; set; }

    public short? Masteryscore { get; set; }

    public DateTime Lastupdatedat { get; set; }

    public virtual Playerprofile Player { get; set; } = null!;

    public virtual Technique Technique { get; set; } = null!;
}
