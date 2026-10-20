using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Cosmetic
{
    public int Cosmeticid { get; set; }

    public int Currencyid { get; set; }

    public string Itemcode { get; set; } = null!;

    public string Cosmeticname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int Price { get; set; }

    public string Type { get; set; } = null!;

    public virtual Currency Currency { get; set; } = null!;

    public virtual ICollection<Playercosmetic> Playercosmetics { get; set; } = new List<Playercosmetic>();
}
