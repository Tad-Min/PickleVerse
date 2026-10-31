using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Verificationemail
{
    public int Verificationemailid { get; set; }

    public Guid Userid { get; set; }

    public string Email { get; set; } = null!;

    public string Token { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime Expiresat { get; set; }

    public bool Isverified { get; set; }

    public int Failedattempts { get; set; }

    public virtual User User { get; set; } = null!;
}
