namespace Entities.Game;

public sealed class OnlineSocialPlayer
{
    public Guid Id;
    public string Name = "", Relationship = "none", Avatar = "";
    public int Elo, Position, Level, RequestId;
}
public sealed class OnlineSocialPage
{
    public List<OnlineSocialPlayer> Players = [];
    public int Total, MyPosition, PlayerCount, FriendCount, Offset;
    public bool HasMore;
}
public sealed class SocialActionRequest
{
    public Guid PlayerId { get; set; }
    public string Action { get; set; } = "";
    public int RequestId { get; set; }
}
