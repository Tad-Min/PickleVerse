using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Matchhistoryreplay
{
    public Guid Matchid { get; set; }

    public string Objectkey { get; set; } = null!;

    public virtual Gamematch Match { get; set; } = null!;
}
