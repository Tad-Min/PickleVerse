using PickleVerse.Progression;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    // This is an API/engine object assembled from domain tables, never a persisted player document.
    async Task LoadProgress(Session s, Guid id)
    {
        var p=s.Player;var row=s.profile;
        s.revision=row.Revision;
        s.state.tutorial=new TutorialState {completed=row.TutorialCompleted,skipped=row.TutorialSkipped,checkpoint=row.TutorialCheckpoint};
        p.totalXp=100L*row.Level*(row.Level-1)+row.Exp;
        p.rankWinStreak=row.RankWinStreak;p.rankLossStreak=row.RankLossStreak;
        p.createdUtcTicks=(await _unitOfWork.Game.AccountCreated(id)).Ticks;
        p.lifetime=new ProgressCounters {matches=row.LifetimeMatches,wins=row.Wincount,points=row.LifetimePoints,
            returns=row.LifetimeReturns,serves=row.LifetimeServes,botMatches=row.LifetimeBotMatches,humanMatches=row.LifetimeHumanMatches,
            humanWins=row.LifetimeHumanWins,rankedWins=row.LifetimeRankedWins,doublesWins=row.LifetimeDoublesWins,
            higherRatedWins=row.LifetimeHigherRatedWins,streak=row.Winstreak,bestStreak=row.BestWinStreak};
        p.achievementClaims=await _unitOfWork.Game.AchievementClaims(id);
        p.training=await _unitOfWork.Game.Training(id);
        await LoadMissionPeriods(s,id);
        p.processedMatches=(await _unitOfWork.Game.ProcessedMatches(id)).Select(x=>x.ToString("N")).ToList();
        var records=await _unitOfWork.Game.MatchHistory(id);
        p.history=records.Select(m=>RelationalMatch.Result(m,m.Gamematchplayers.Single(x=>x.Playerid==id))).ToList();
        p.dailyStreak=new DailyStreakData {enabledUtcTicks=row.DailyStreakEnabledAt?.Ticks??0};
        var enabled=row.DailyStreakEnabledAt??Now;
        var days=await _unitOfWork.Game.PlayedDays(id, enabled);
        p.dailyStreak.days=days.Select(x=>x.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture)).ToList();
        DailyStreakEngine.Refresh(p,Now);
        var pending=await _unitOfWork.Game.PendingMatch(id);
        if(pending!=null)
        {
            p.pendingMatch=RelationalMatch.Result(pending,pending.Gamematchplayers.Single(x=>x.Playerid==id),includeReport:false);
        }
    }

    async Task LoadMissionPeriods(Session s, Guid player)
    {
        var definitions=s.catalog.definitions.Where(d=>d.scope!=ProgressScope.Achievement).ToDictionary(d=>d.id);
        var rows=await _unitOfWork.Game.MissionPeriods(player);
        var periods=new Dictionary<(ProgressScope,string),MissionPeriod>();
        foreach(var row in rows)
        {
            if(!definitions.TryGetValue(row.code,out var def))continue;
            s.missionRows[(def.scope,row.period,row.code)]=(row.progress,row.claimed,row.streak);
            var key=(def.scope,row.period);
            if(!periods.TryGetValue(key,out var period))periods[key]=period=new MissionPeriod {key=row.period};
            SetCounter(period.counters,def.metric,row.progress);
            if(def.metric==ProgressMetric.BestStreak)period.counters.streak=row.streak;
            if(row.claimed)period.claimed.Add(row.code);
        }
        var bonuses=await _unitOfWork.Game.BonusCommands(player);
        s.bonusCommands=bonuses.ToHashSet();
        foreach(var (key,period) in periods)
            period.bonusClaimed=bonuses.Contains(BonusCommand(key.Item1,key.Item2));
        foreach(var scope in new[]{ProgressScope.Daily,ProgressScope.Weekly})
        {
            var all=periods.Where(x=>x.Key.Item1==scope).Select(x=>x.Value).OrderByDescending(x=>x.key).ToList();
            var current=all.FirstOrDefault()??new MissionPeriod();
            if(scope==ProgressScope.Daily){s.Player.daily=current;s.Player.previousDays=all.Skip(1).ToList();}
            else {s.Player.weekly=current;s.Player.previousWeeks=all.Skip(1).ToList();}
        }
    }
    static string BonusCommand(ProgressScope scope,string period)=>$"period-bonus:{scope}:{period}";
    static void SetCounter(ProgressCounters c,ProgressMetric metric,long value)
    {
        switch(metric)
        {
            case ProgressMetric.Matches:c.matches=Math.Max(c.matches,value);break;
            case ProgressMetric.Wins:c.wins=Math.Max(c.wins,value);break;
            case ProgressMetric.Points:c.points=Math.Max(c.points,value);break;
            case ProgressMetric.Returns:c.returns=Math.Max(c.returns,value);break;
            case ProgressMetric.Serves:c.serves=Math.Max(c.serves,value);break;
            case ProgressMetric.BotMatches:c.botMatches=Math.Max(c.botMatches,value);break;
            case ProgressMetric.HumanMatches:c.humanMatches=Math.Max(c.humanMatches,value);break;
            case ProgressMetric.HumanWins:c.humanWins=Math.Max(c.humanWins,value);break;
            case ProgressMetric.RankedWins:c.rankedWins=Math.Max(c.rankedWins,value);break;
            case ProgressMetric.DoublesWins:c.doublesWins=Math.Max(c.doublesWins,value);break;
            case ProgressMetric.HigherRatedWins:c.higherRatedWins=Math.Max(c.higherRatedWins,value);break;
            default:c.bestStreak=Math.Max(c.bestStreak,(int)value);break;
        }
    }

    async Task PersistProgress(Session s,Guid id)
    {
        var p=s.Player;var row=s.profile;var c=p.lifetime;
        row.TutorialCompleted=s.state.tutorial.completed;row.TutorialSkipped=s.state.tutorial.skipped;row.TutorialCheckpoint=s.state.tutorial.checkpoint;
        row.RankWinStreak=p.rankWinStreak;row.RankLossStreak=p.rankLossStreak;row.BestWinStreak=c.bestStreak;
        row.LifetimeMatches=c.matches;row.LifetimePoints=c.points;row.LifetimeReturns=c.returns;row.LifetimeServes=c.serves;
        row.LifetimeBotMatches=c.botMatches;row.LifetimeHumanMatches=c.humanMatches;row.LifetimeHumanWins=c.humanWins;
        row.LifetimeRankedWins=c.rankedWins;row.LifetimeDoublesWins=c.doublesWins;row.LifetimeHigherRatedWins=c.higherRatedWins;
        if(p.dailyStreak?.enabledUtcTicks>0)row.DailyStreakEnabledAt=new DateTime(p.dailyStreak.enabledUtcTicks,DateTimeKind.Utc);
        s.revision=++row.Revision;
        if(s.trainingChanged)await _unitOfWork.Game.SaveTraining(id,p.training);
        foreach(var scope in new[]{ProgressScope.Daily,ProgressScope.Weekly})
        {
            var archive=scope==ProgressScope.Daily?p.previousDays:p.previousWeeks;
            foreach(var period in archive.Append(ProgressionEngine.Period(p,scope)).Where(x=>x.key.Length>0))
            {
                foreach(var def in s.catalog.definitions.Where(d=>d.scope==scope))
                {
                    var value=((int)Math.Min(int.MaxValue,period.counters.Get(def.metric)),period.claimed.Contains(def.id),def.metric==ProgressMetric.BestStreak?period.counters.streak:0);
                    var key=(scope,period.key,def.id);
                    if(s.missionRows.TryGetValue(key,out var previous) && previous==value)continue;
                    await _unitOfWork.Game.SaveMission(id, def.id, period.key, value.Item1, value.Item2, value.Item3);
                    s.missionRows[key]=value;
                }
                if(period.bonusClaimed && s.bonusCommands.Add(BonusCommand(scope,period.key)))
                    _unitOfWork.Game.AddCommand(id, BonusCommand(scope,period.key), "period-bonus", Hash(BonusCommand(scope,period.key)), GameJson.Payload(new {scope,period.key}));
            }
        }
    }
}
