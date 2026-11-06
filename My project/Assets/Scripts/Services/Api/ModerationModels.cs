using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PickleVerse.Services.Api
{
    public sealed class WireStatusConverter : StringEnumConverter
    { public WireStatusConverter() { AllowIntegerValues = false; } }
    [JsonConverter(typeof(WireStatusConverter))]
    public enum SessionAccessScope { [EnumMember(Value = "game")] Game, [EnumMember(Value = "appeals")] Appeals }
    [JsonConverter(typeof(WireStatusConverter))]
    public enum UserAccountStatus { [EnumMember(Value = "ACTIVE")] Active, [EnumMember(Value = "BANNED")] Banned, [EnumMember(Value = "SUSPENDED")] Suspended }
    [JsonConverter(typeof(WireStatusConverter))]
    public enum AppealReviewStatus
    { [EnumMember(Value = "PENDING")] Pending, [EnumMember(Value = "CHECKING")] Checking, [EnumMember(Value = "ACCEPTED")] Accepted, [EnumMember(Value = "DECLINED")] Declined }
    [JsonConverter(typeof(WireStatusConverter))]
    public enum DedicatedTicketState
    {
        [EnumMember(Value = "queued")] Queued, [EnumMember(Value = "starting")] Starting, [EnumMember(Value = "ready")] Ready,
        [EnumMember(Value = "finishing")] Finishing, [EnumMember(Value = "pending")] Pending, [EnumMember(Value = "busy")] Busy
    }
    [JsonConverter(typeof(WireStatusConverter))]
    public enum RankedSearchState
    {
        [EnumMember(Value = "SEARCHING")] Searching, [EnumMember(Value = "MATCHED")] Matched, [EnumMember(Value = "BOT")] Bot,
        [EnumMember(Value = "CANCELLED")] Cancelled, [EnumMember(Value = "TIMEOUT")] TimedOut
    }
    [Serializable] public sealed class SanctionDecision
    {
        public Guid SanctionId, UserId, ActorId;
        public UserAccountStatus Restriction;
        public string Reason, RevocationReason;
        public DateTime CreatedAt;
        public DateTime? ExpiresAt, RevokedAt;
        public bool Active => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
    }
    [Serializable] public sealed class PlayerAppeal
    {
        public Guid AppealId, SanctionId;
        public string Statement, DecisionReason;
        public AppealReviewStatus Status;
        public DateTime CreatedAt, UpdatedAt;
    }
    [Serializable] public sealed class ReportTarget
    { public Guid PlayerId; public string PlayerName; public int Team; }
}
