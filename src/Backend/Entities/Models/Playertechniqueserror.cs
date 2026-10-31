using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playertechniqueserror
{
    public int Errorlogid { get; set; }

    public Guid Playerid { get; set; }

    public int Techniqueid { get; set; }

    public Guid? Matchid { get; set; }

    public int? Lessonid { get; set; }

    public string Errortype { get; set; } = null!;

    public string Coachingtip { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public virtual Lesson? Lesson { get; set; }

    public virtual Gamematch? Match { get; set; }

    public virtual Playerprofile Player { get; set; } = null!;

    public virtual Technique Technique { get; set; } = null!;
}
