using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playercosmetic
{
    public Guid Playerid { get; set; }

    public int Cosmeticid { get; set; }

    public bool Equipped { get; set; }

    public virtual Cosmetic Cosmetic { get; set; } = null!;

    public virtual Playerprofile Player { get; set; } = null!;
}
