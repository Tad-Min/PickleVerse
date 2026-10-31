using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Drill
{
    public int Drillid { get; set; }

    public int Techniqueid { get; set; }

    public string Drillname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<Lessondrill> Lessondrills { get; set; } = new List<Lessondrill>();


    public virtual Technique Technique { get; set; } = null!;
}
