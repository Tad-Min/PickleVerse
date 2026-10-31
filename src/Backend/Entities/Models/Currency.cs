using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Currency
{
    public int Currencyid { get; set; }

    public string Currencyname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<Cosmetic> Cosmetics { get; set; } = new List<Cosmetic>();

    public virtual ICollection<Playercurrency> Playercurrencies { get; set; } = new List<Playercurrency>();
}
