using System.Diagnostics.Metrics;

namespace Services.Game;

public static class HybridAiMetrics
{
    public static readonly Meter Meter=new("PickleVerse.HybridAi","1.0");
    public static readonly Histogram<double> IngestStageMs=Meter.CreateHistogram<double>("hybrid.ingest.stage.ms","ms");
    public static readonly Histogram<double> AnalysisMs=Meter.CreateHistogram<double>("hybrid.analysis.ms","ms");
    public static readonly Counter<long> Completed=Meter.CreateCounter<long>("hybrid.jobs.completed");
    public static readonly Counter<long> Retried=Meter.CreateCounter<long>("hybrid.jobs.retried");
}
