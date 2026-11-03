namespace Entities.Configuration;

public sealed class DedicatedServerSettings
{
    public bool Enabled { get; set; }
    public string ServiceKey { get; set; } = "";
    public int Protocol { get; set; } = 18;
    public int LeaseSeconds { get; set; } = 45;
}
