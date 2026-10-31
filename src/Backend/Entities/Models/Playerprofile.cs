using System;
using System.Collections.Generic;

namespace Entities.Models;

public partial class Playerprofile
{
    public Guid Userid { get; set; }

    public string Avatar { get; set; } = null!;

    public string Playername { get; set; } = null!;

    public int Elo { get; set; }

    public bool Ismale { get; set; }

    public long Timeplayedseconds { get; set; }

    public int Level { get; set; }

    public int Exp { get; set; }

    public int Winstreak { get; set; }

    public int Daystreak { get; set; }

    public int Wincount { get; set; }

    public int Losecount { get; set; }

    public long Revision { get; set; }

    public bool TutorialCompleted { get; set; }

    public bool TutorialSkipped { get; set; }

    public DateTime? DailyStreakEnabledAt { get; set; }

    public int RankWinStreak { get; set; }

    public int RankLossStreak { get; set; }

    public int BestWinStreak { get; set; }

    public long LifetimeMatches { get; set; }

    public long LifetimePoints { get; set; }

    public long LifetimeReturns { get; set; }

    public long LifetimeServes { get; set; }

    public long LifetimeBotMatches { get; set; }

    public long LifetimeHumanMatches { get; set; }

    public long LifetimeHumanWins { get; set; }

    public long LifetimeRankedWins { get; set; }

    public long LifetimeDoublesWins { get; set; }

    public long LifetimeHigherRatedWins { get; set; }

    public virtual ICollection<Playerachievement> Playerachievements { get; set; } = new List<Playerachievement>();

    public virtual ICollection<Playercosmetic> Playercosmetics { get; set; } = new List<Playercosmetic>();

    public virtual ICollection<Playercurrency> Playercurrencies { get; set; } = new List<Playercurrency>();

    public virtual ICollection<Playerlessondrill> Playerlessondrills { get; set; } = new List<Playerlessondrill>();

    public virtual ICollection<Gamematchplayer> Gamematchplayers { get; set; } = new List<Gamematchplayer>();

    public virtual ICollection<Playermission> Playermissions { get; set; } = new List<Playermission>();

    public virtual ICollection<Playertechniquemastery> Playertechniquemasteries { get; set; } = new List<Playertechniquemastery>();

    public virtual ICollection<Playertechniqueserror> Playertechniqueserrors { get; set; } = new List<Playertechniqueserror>();

    public virtual ICollection<Reportplayerinmatch> ReportplayerinmatchReportedplayers { get; set; } = new List<Reportplayerinmatch>();

    public virtual ICollection<Reportplayerinmatch> ReportplayerinmatchReporters { get; set; } = new List<Reportplayerinmatch>();

    public virtual User User { get; set; } = null!;
}
