using PickleVerse.Progression;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    // This is an API/engine object assembled from domain tables, never a persisted player document.
    async Task LoadProgress(Session s, Guid id)
    {
        var p=s.Player;var row=s.profile;
        s.revision=row.Revision;
        s.state.tutorial=new TutorialState {completed=row.TutorialCompleted,skipped=row.TutorialSkipped};
        p.totalXp=100L*row.Level*(row.Level-1)+row.Exp;
        p.rankWinStreak=row.RankWinStreak;p.rankLossStreak=row.RankLossStreak;
        p.createdUtcTicks=await sql.One("SELECT createdat FROM users WHERE userid=@player",r=>r.GetDateTime(0).Ticks,P("player",id));
        p.lifetime=new ProgressCounters {matches=row.LifetimeMatches,wins=row.Wincount,points=row.LifetimePoints,
            returns=row.LifetimeReturns,serves=row.LifetimeServes,botMatches=row.LifetimeBotMatches,humanMatches=row.LifetimeHumanMatches,
            humanWins=row.LifetimeHumanWins,rankedWins=row.LifetimeRankedWins,doublesWins=row.LifetimeDoublesWins,
            higherRatedWins=row.LifetimeHigherRatedWins,streak=row.Winstreak,bestStreak=row.BestWinStreak};
        p.achievementClaims=await sql.Query("SELECT a.achievementcode FROM playerachievements p JOIN achievements a USING(achievementid) WHERE p.playerid=@player AND p.isclaimed",r=>r.GetString(0),P("player",id));
        p.training=await RelationalTraining.Load(sql,id);
        await LoadMissionPeriods(s,id);
        p.processedMatches=await sql.Query("SELECT replace(matchid::text,'-','') FROM playermatchhistory WHERE playerid=@player",r=>r.GetString(0),P("player",id));
        p.history=await sql.Query("""
            SELECT result::text FROM (
                SELECT h.createdat,h.sequence,CASE WHEN m.kind='BotPractice' THEN m.data->'result' ELSE m.data->'results'->@key END AS result
                FROM playermatchhistory p JOIN matchhistory h USING(matchid) JOIN gamematches m USING(matchid) WHERE p.playerid=@player
            ) results WHERE result IS NOT NULL ORDER BY createdat DESC,sequence DESC LIMIT 20
            """,r=>GameJson.Read<MatchResult>(r.GetString(0)),P("player",id),P("key",id.ToString("N")));
        p.dailyStreak=new DailyStreakData {enabledUtcTicks=row.DailyStreakEnabledAt?.Ticks??0};
        p.dailyStreak.days=await sql.Query("""
            SELECT DISTINCT to_char(h.createdat AT TIME ZONE 'UTC','YYYYMMDD') AS day
            FROM playermatchhistory p JOIN matchhistory h USING(matchid) JOIN gamematches m USING(matchid)
            WHERE p.playerid=@player AND p.status IN ('WIN','LOSE') AND h.createdat>=@enabled
                AND NOT COALESCE((CASE WHEN m.kind='BotPractice' THEN m.data->'result' ELSE m.data->'results'->@key END->>'localForfeit')::boolean,false)
            ORDER BY day
            """,r=>r.GetString(0),P("player",id),P("key",id.ToString("N")),P("enabled",row.DailyStreakEnabledAt??Now));
        DailyStreakEngine.Refresh(p,Now);
        var pending=await sql.One("""
            SELECT m.kind,m.data::text FROM gamematches m JOIN gamematchplayers p USING(matchid)
            WHERE p.playerid=@player AND m.status='STARTED' AND
                ((m.kind='BotPractice' AND m.data->'result'->>'playerId'=@key) OR m.data->>'phase'='PLAYING')
            ORDER BY m.createdat DESC,m.matchid DESC LIMIT 1
            """,r=>new {kind=r.GetString(0),data=r.GetString(1)},P("player",id),P("key",id.ToString("N")));
        if(pending!=null)
        {
            if(pending.kind=="BotPractice")p.pendingMatch=GameJson.Read<StoredMatch>(pending.data).result;
            else {var match=GameJson.Read<OnlineMatch>(pending.data);p.pendingMatch=OnlineResult(match,match.roster.Find(id.ToString("N")));}
        }
    }

    async Task LoadMissionPeriods(Session s, Guid player)
    {
        var definitions=s.catalog.definitions.Where(d=>d.scope!=ProgressScope.Achievement).ToDictionary(d=>d.id);
        var rows=await sql.Query("""
            SELECT m.code,p.periodkey,p.progress,p.isclaimed,p.currentstreak FROM playermissions p JOIN missions m USING(missionid)
            WHERE p.playerid=@player AND p.periodkey<>'' ORDER BY p.periodkey
            """,r=>new {code=r.GetString(0),period=r.GetString(1),progress=r.GetInt32(2),claimed=r.GetBoolean(3),streak=r.GetInt32(4)},P("player",player));
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
        var bonuses=await sql.Query("SELECT commandid FROM gamecommands WHERE playerid=@player AND kind='period-bonus'",r=>r.GetString(0),P("player",player));
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
        row.TutorialCompleted=s.state.tutorial.completed;row.TutorialSkipped=s.state.tutorial.skipped;
        row.RankWinStreak=p.rankWinStreak;row.RankLossStreak=p.rankLossStreak;row.BestWinStreak=c.bestStreak;
        row.LifetimeMatches=c.matches;row.LifetimePoints=c.points;row.LifetimeReturns=c.returns;row.LifetimeServes=c.serves;
        row.LifetimeBotMatches=c.botMatches;row.LifetimeHumanMatches=c.humanMatches;row.LifetimeHumanWins=c.humanWins;
        row.LifetimeRankedWins=c.rankedWins;row.LifetimeDoublesWins=c.doublesWins;row.LifetimeHigherRatedWins=c.higherRatedWins;
        if(p.dailyStreak?.enabledUtcTicks>0)row.DailyStreakEnabledAt=new DateTime(p.dailyStreak.enabledUtcTicks,DateTimeKind.Utc);
        s.revision=++row.Revision;
        if(s.trainingChanged)await RelationalTraining.Save(sql,id,p.training);
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
                    await sql.Run("""
                        INSERT INTO playermissions(playerid,missionid,periodkey,progress,isclaimed,claimedat,currentstreak)
                        SELECT @id,missionid,@period,@progress,@claimed,CASE WHEN @claimed THEN CURRENT_TIMESTAMP ELSE NULL END,@streak FROM missions WHERE code=@code
                        ON CONFLICT(playerid,missionid,periodkey) DO UPDATE SET progress=EXCLUDED.progress,isclaimed=EXCLUDED.isclaimed,
                            claimedat=COALESCE(playermissions.claimedat,EXCLUDED.claimedat),currentstreak=EXCLUDED.currentstreak
                        """,P("id",id),P("period",period.key),P("progress",value.Item1),
                        P("claimed",value.Item2),P("streak",value.Item3),P("code",def.id));
                    s.missionRows[key]=value;
                }
                if(period.bonusClaimed && s.bonusCommands.Add(BonusCommand(scope,period.key)))
                    await sql.Run("""
                        INSERT INTO gamecommands(playerid,commandid,kind,fingerprint,result) VALUES(@id,@command,'period-bonus',@fingerprint,@result::jsonb)
                        ON CONFLICT(playerid,commandid) DO NOTHING
                        """,P("id",id),P("command",BonusCommand(scope,period.key)),P("fingerprint",Hash(BonusCommand(scope,period.key))),P("result",GameJson.Payload(new {scope,period.key})));
            }
        }
    }
}
