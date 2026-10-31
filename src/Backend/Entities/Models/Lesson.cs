using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Lesson
{
    public int Lessonid { get; set; }

    public string Lessonname { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int Target { get; set; }

    public string Reward { get; set; } = null!;

    public virtual ICollection<Lessondrill> Lessondrills { get; set; } = new List<Lessondrill>();


    public virtual ICollection<Playertechniqueserror> Playertechniqueserrors { get; set; } = new List<Playertechniqueserror>();
}
