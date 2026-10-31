using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Lessondrill
{
    public int Lessonid { get; set; }

    public int Drillid { get; set; }

    public int Target { get; set; }

    public virtual ICollection<Playerlessondrill> Playerlessondrills { get; set; } = new List<Playerlessondrill>();

    public virtual Drill Drill { get; set; } = null!;

    public virtual Lesson Lesson { get; set; } = null!;
}
