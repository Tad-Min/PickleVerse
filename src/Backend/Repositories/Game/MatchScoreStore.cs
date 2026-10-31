using PickleVerse.Progression;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.Game;

// One score per game and match. Player-facing scores are oriented using the participant's team.
public static class MatchScoreStore
{
    // MatchScore is the sole persisted source of game wins. An unfinished game
    // contributes points but cannot count as a won/lost game, including forfeits.
    public static (int teamA, int teamB) GameWins(IEnumerable<Matchscore> scores) =>
        (scores.Count(s => s.Completed && s.Teamascore > s.Teambscore),
         scores.Count(s => s.Completed && s.Teambscore > s.Teamascore));

    // Called inside the same transaction that commits the match result and rewards.
    public static async Task Save(PickleVerseContext db,Guid match,IReadOnlyList<GameScore> scores)
    {
        if(scores.Count>3)throw new InvalidOperationException("A match has at most three games.");
        for(int i=0;i<scores.Count;i++)
        {
            var game=i+1;var a=scores[i].own;var b=scores[i].opponent;var complete=scores[i].completed;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO matchscore(matchid,gamenumber,teamascore,teambscore,completed)
                VALUES({match},{game},{a},{b},{complete})
                ON CONFLICT(matchid,gamenumber) DO UPDATE SET teamascore=EXCLUDED.teamascore,
                    teambscore=EXCLUDED.teambscore,completed=EXCLUDED.completed
                """);
        }
        await db.Matchscores.Where(s=>s.Matchid==match && s.Gamenumber>scores.Count).ExecuteDeleteAsync();
    }

    public static async Task Hydrate(PickleVerseContext db,Guid player,IEnumerable<MatchResult> results)
    {
        var matches=results.ToList();
        if(matches.Count==0)return;
        var ids=matches.Select(m=>Guid.Parse(m.id)).Distinct().ToArray();
        var rows=await db.Matchscores.AsNoTracking().Where(s=>ids.Contains(s.Matchid))
            .Join(db.Gamematchplayers.Where(p=>p.Playerid==player),s=>s.Matchid,p=>p.Matchid,
                (s,p)=>new {match=s.Matchid,game=s.Gamenumber,team=p.Team,a=s.Teamascore,b=s.Teambscore,completed=s.Completed})
            .OrderBy(r=>r.match).ThenBy(r=>r.game).ToListAsync();
        var grouped=rows.GroupBy(r=>r.match).ToDictionary(g=>g.Key,g=>g.Select(r=>new GameScore(r.team==0?r.a:r.b,r.team==0?r.b:r.a,r.completed)).ToList());
        foreach(var match in matches)match.scores=grouped.GetValueOrDefault(Guid.Parse(match.id))??[];
    }
}
