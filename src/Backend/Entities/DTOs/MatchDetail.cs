namespace Entities.DTOs;

public sealed record MatchDetail(Guid MatchId, string Kind, int Mode, DateTime EndedAt, int? EloDelta,
    int TeamAWins, int TeamBWins, string Reason, IReadOnlyList<MatchDetailGame> Games, IReadOnlyList<MatchDetailPlayer> Players);
public sealed record MatchDetailGame(int GameNumber, int TeamAScore, int TeamBScore, bool Completed, int? TeamAStartSide);
public sealed record MatchDetailPlayer(Guid PlayerId, string Name, int Team, bool CanReport,
    string AnalysisState, IReadOnlyList<MatchGameTags> Games);
public sealed record MatchGameTags(int GameNumber, IReadOnlyList<MatchTag> Metrics);
public sealed record MatchTag(string Technique, string Code, int Errors, int Opportunities);
