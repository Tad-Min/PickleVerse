using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playerlessondrill
{
    public Guid Playerid { get; set; }

    public int Lessonid { get; set; }

    public int Drillid { get; set; }

    public int Score { get; set; }

    public bool Isclaimedreward { get; set; }

    public virtual Drill Drill { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;

    public virtual Playerprofile Player { get; set; } = null!;
}
