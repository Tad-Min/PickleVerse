namespace Entities.Models;

public sealed class Passwordresetchallenge
{
    public Guid Userid { get; set; }
    public Guid Challengeid { get; set; }
    public string Tokenhash { get; set; } = "";
    public DateTime Createdat { get; set; }
    public DateTime Expiresat { get; set; }
    public int Failedattempts { get; set; }
    public bool Consumed { get; set; }
}
