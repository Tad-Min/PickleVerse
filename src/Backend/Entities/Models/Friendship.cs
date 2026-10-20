using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Friendship
{
    public Guid Userid1 { get; set; }

    public Guid Userid2 { get; set; }

    public DateTime Createdat { get; set; }

    public virtual User Userid1Navigation { get; set; } = null!;

    public virtual User Userid2Navigation { get; set; } = null!;
}
