using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Entities.Enums
{
    [JsonConverter(typeof(StatusJsonConverter<UserStatus>))]
    public enum UserStatus
    {
        [EnumMember(Value = "ACTIVE")] Active,
        [EnumMember(Value = "BANNED")] Banned,
        [EnumMember(Value = "SUSPENDED")] Suspended
    }

    [JsonConverter(typeof(StatusJsonConverter<FriendRequestStatus>))]
    public enum FriendRequestStatus
    {
        [EnumMember(Value = "PENDING")] Pending,
        [EnumMember(Value = "ACCEPTED")] Accepted,
        [EnumMember(Value = "DECLINED")] Declined
    }

    [JsonConverter(typeof(StatusJsonConverter<ReportStatus>))]
    public enum ReportStatus
    {
        [EnumMember(Value = "PENDING")] Pending,
        [EnumMember(Value = "CHECKING")] Checking,
        [EnumMember(Value = "ACCEPTED")] Accepted,
        [EnumMember(Value = "DECLINED")] Declined
    }

    [JsonConverter(typeof(StatusJsonConverter<MatchStatus>))]
    public enum MatchStatus
    {
        [EnumMember(Value = "PREPARING")] Preparing,
        [EnumMember(Value = "PLAYING")] Playing,
        [EnumMember(Value = "FINISHED")] Finished,
        [EnumMember(Value = "CANCELLED")] Cancelled
    }

    [JsonConverter(typeof(StatusJsonConverter<MatchAuthorityMode>))]
    public enum MatchAuthorityMode
    {
        [EnumMember(Value = "shared")] Shared,
        [EnumMember(Value = "dedicated")] Dedicated
    }

    [JsonConverter(typeof(StatusJsonConverter<ContentPublishStatus>))]
    public enum ContentPublishStatus
    {
        [EnumMember(Value = "DRAFT")] Draft,
        [EnumMember(Value = "PUBLISHED")] Published,
        [EnumMember(Value = "UNPUBLISHED")] Unpublished
    }

    [JsonConverter(typeof(StatusJsonConverter<AppealStatus>))]
    public enum AppealStatus
    {
        [EnumMember(Value = "PENDING")] Pending,
        [EnumMember(Value = "CHECKING")] Checking,
        [EnumMember(Value = "ACCEPTED")] Accepted,
        [EnumMember(Value = "DECLINED")] Declined
    }

    [JsonConverter(typeof(StatusJsonConverter<SyncItemStatus>))]
    public enum SyncItemStatus
    {
        [EnumMember(Value = "APPLIED")] Applied,
        [EnumMember(Value = "DUPLICATE")] Duplicate,
        [EnumMember(Value = "REJECTED")] Rejected
    }

    [JsonConverter(typeof(StatusJsonConverter<ProgressResetGroup>))]
    public enum ProgressResetGroup
    {
        [EnumMember(Value = "LEARNING")] Learning,
        [EnumMember(Value = "RANKING")] Ranking,
        [EnumMember(Value = "MISSIONS")] Missions,
        [EnumMember(Value = "ECONOMY")] Economy
    }

    [JsonConverter(typeof(StatusJsonConverter<DedicatedAllocationStatus>))]
    public enum DedicatedAllocationStatus
    {
        [EnumMember(Value = "queued")] Queued,
        [EnumMember(Value = "starting")] Starting,
        [EnumMember(Value = "ready")] Ready,
        [EnumMember(Value = "finishing")] Finishing,
        [EnumMember(Value = "pending")] Pending,
        [EnumMember(Value = "busy")] Busy
    }

    [JsonConverter(typeof(StatusJsonConverter<SessionScope>))]
    public enum SessionScope
    {
        [EnumMember(Value = "game")] Game,
        [EnumMember(Value = "appeals")] Appeals
    }

    [JsonConverter(typeof(StatusJsonConverter<MatchmakingSearchStatus>))]
    public enum MatchmakingSearchStatus
    {
        [EnumMember(Value = "SEARCHING")] Searching,
        [EnumMember(Value = "MATCHED")] Matched,
        [EnumMember(Value = "BOT")] Bot,
        [EnumMember(Value = "CANCELLED")] Cancelled,
        [EnumMember(Value = "TIMEOUT")] TimedOut
    }

    public enum MatchResultStatus
    {
        Win,
        Loss,
        Draw
    }

    public enum MatchType
    {
        Singles1v1, // "1Vs1"
        Doubles2v2  // "2Vs2"
    }

    public enum LessonStatus
    {
        Locked,
        Unlocked,
        Completed
    }

    public enum TechniqueType
    {
        Serve,
        Dink,
        Volley,
        ThirdShotDrop,
        KitchenRule
    }

    public enum TechniqueErrorCode
    {
        LateServe,
        HighDink,
        KitchenViolation,
        MissedContact,
        DeepOut
    }
}
