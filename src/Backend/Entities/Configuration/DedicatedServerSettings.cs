namespace Entities.Configuration;

public sealed class DedicatedServerSettings
{
    public const int MaximumConcurrentMatches = 500;
    public bool Enabled { get; set; }
    public string ServiceKey { get; set; } = "";
    public int Protocol { get; set; } = 19;
    public int LeaseSeconds { get; set; } = 45;
    public int MaxConcurrentMatches { get; set; } = 1;
}
