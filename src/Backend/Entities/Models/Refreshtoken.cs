using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Refreshtoken
{
    public int Tokenid { get; set; }

    public Guid Userid { get; set; }

    public string Tokenhash { get; set; } = null!;

    public DateTime Expiresat { get; set; }

    public bool Isrevoked { get; set; }

    public DateTime Createdat { get; set; }

    public virtual User User { get; set; } = null!;
}
