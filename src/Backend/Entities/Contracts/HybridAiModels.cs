#nullable disable
#nullable disable
using System;
using System.Collections.Generic;

namespace PickleVerse.Progression
{
    [Serializable]
    public sealed class BotPolicy
    {
        public string version = "bot-v1";
        public BotTierProfile[] tiers = {
            new BotTierProfile { minimumElo = 100, reactionSeconds = .28f, aimError = .30f, aggressiveness = .25f },
            new BotTierProfile { minimumElo = 500, reactionSeconds = .15f, aimError = .16f, aggressiveness = .50f },
            new BotTierProfile { minimumElo = 2000, reactionSeconds = .08f, aimError = .08f, aggressiveness = .75f }
        };
        public BotTierProfile Profile(int elo)
        {
            var selected = tiers[0];
            foreach (var tier in tiers) if (elo >= tier.minimumElo) selected = tier;
            return new BotTierProfile { minimumElo = selected.minimumElo, reactionSeconds = selected.reactionSeconds,
                aimError = selected.aimError, aggressiveness = selected.aggressiveness };
        }
    }
    [Serializable]
    public sealed class BotTierProfile
    {
        public int minimumElo;
        public float reactionSeconds, aimError, aggressiveness;
    }
    [Serializable]
    public sealed class ShotTelemetry
    {
        public string shotId = "", kind = "", technique = "", source = "", outcome = "unknown";
        public int sequence, rally;
        public double time;
        // Signed seconds: negative = early, positive = late. Null means unobserved.
        public float? timingError;
        public float[] contact, leftFoot, rightFoot, intendedTarget, landing;
        public bool? kitchen, volley, momentum, recovered, targetHit;
        public int? bounces;
        public float? netClearance;
        public bool engineAimed = true, assisted = true;
    }
    [Serializable]
    public sealed class TelemetryBatch
    {
        public string batchId = "", sessionId = "", sessionType = "", playerId = "", gameVersion = "", schemaVersion = "1";
        public int partIndex, partCount = 1;
        public bool partial;
        public List<ShotTelemetry> shots = new List<ShotTelemetry>();
    }
    [Serializable]
    public sealed class TelemetryReceipt
    {
        public string batchId = "", status = "";
        public int acceptedParts;
    }
    public static class TelemetryBatching
    {
        // Measure each shot once. Re-serializing a growing part makes long sessions quadratic.
        public static List<TelemetryBatch> Split(TelemetryBatch envelope, IList<ShotTelemetry> shots, Func<ShotTelemetry, int> byteCount)
        {
            var result = new List<TelemetryBatch>();
            TelemetryBatch NewPart() => new TelemetryBatch { batchId = envelope.batchId, sessionId = envelope.sessionId,
                playerId = envelope.playerId, sessionType = envelope.sessionType, gameVersion = envelope.gameVersion,
                schemaVersion = envelope.schemaVersion, partial = envelope.partial };
            var part = NewPart();
            int bytes = 2048;
            foreach (var shot in shots)
            {
                int size = byteCount(shot) + 1;
                if (size > 997952) throw new ArgumentException("Shot telemetry exceeds part limit.");
                if (part.shots.Count == 1000 || bytes + size > 1000000)
                {
                    result.Add(part); part = NewPart(); bytes = 2048;
                }
                part.shots.Add(shot); bytes += size;
            }
            result.Add(part);
            if (result.Count > 100) throw new ArgumentException("Telemetry exceeds session part limit.");
            for (int i = 0; i < result.Count; i++) { result[i].partIndex = i; result[i].partCount = result.Count; }
            return result;
        }
    }
    [Serializable]
    public sealed class CoachingFinding
    {
        public string shotId = "", technique = "", code = "", tip = "", ruleVersion = "", evidence = "";
        public int severity;
    }
    [Serializable]
    public sealed class TechniqueMetric
    {
        public string technique = "", code = "";
        public int opportunities, errors, severity;
    }
    [Serializable]
    public sealed class DrillRecommendation
    {
        public string id = "", title = "", reason = "";
    }
    [Serializable]
    public sealed class TierEstimate
    {
        public string status = "insufficient_data", tier, modelVersion;
        public double confidence;
        public int opportunities;
    }
    [Serializable]
    public sealed class CoachingAnalysis
    {
        public string sessionId = "", ruleVersion = "rules-v1", summary = "";
        public List<CoachingFinding> findings = new List<CoachingFinding>();
        public List<TechniqueMetric> metrics = new List<TechniqueMetric>();
        public List<DrillRecommendation> recommendations = new List<DrillRecommendation>();
        public TierEstimate tier = new TierEstimate();
    }
    [Serializable]
    public sealed class CoachingSummary
    {
        public string status = "empty", updatedAt;
        public CoachingAnalysis analysis;
    }
}
