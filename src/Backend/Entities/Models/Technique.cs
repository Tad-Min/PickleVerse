using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Technique
{
    public int Techniqueid { get; set; }

    public string Techniquename { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<Drill> Drills { get; set; } = new List<Drill>();

    public virtual ICollection<Playertechniquemastery> Playertechniquemasteries { get; set; } = new List<Playertechniquemastery>();

    public virtual ICollection<Playertechniqueserror> Playertechniqueserrors { get; set; } = new List<Playertechniqueserror>();
}
