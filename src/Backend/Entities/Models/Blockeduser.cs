using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Blockeduser
{
    public Guid Userid { get; set; }

    public Guid Blockeduserid { get; set; }

    public DateTime Createdat { get; set; }

    public virtual User BlockeduserNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
