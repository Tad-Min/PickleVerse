using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playercurrency
{
    public Guid Playerid { get; set; }

    public int Currencyid { get; set; }

    public int Amount { get; set; }

    public virtual Currency Currency { get; set; } = null!;

    public virtual Playerprofile Player { get; set; } = null!;
}
