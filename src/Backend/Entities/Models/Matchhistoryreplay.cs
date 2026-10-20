using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Matchhistoryreplay
{
    public Guid Matchid { get; set; }

    public string Replayurl { get; set; } = null!;

    public virtual Matchhistory Match { get; set; } = null!;
}
