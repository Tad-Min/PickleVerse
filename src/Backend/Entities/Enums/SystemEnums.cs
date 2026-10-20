namespace Entities.Enums
{
    public enum UserStatus
    {
        Active,
        Banned,
        Suspended
    }

    public enum FriendRequestStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public enum ReportStatus
    {
        Pending,
        Checking,
        Accepted,
        Declined
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
