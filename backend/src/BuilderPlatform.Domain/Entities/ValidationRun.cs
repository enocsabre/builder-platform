namespace BuilderPlatform.Domain.Entities;

public class ValidationRun
{
    public Guid      Id              { get; set; } = Guid.NewGuid();
    public Guid      ProductId       { get; set; }
    public string    Status          { get; set; } = "running"; // running | passed | failed
    public DateTime  StartedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt      { get; set; }
    public string?   Logs            { get; set; }
    public string?   Errors          { get; set; }
    public int       AutofixAttempts { get; set; } = 0;
    public int       GatesPassed     { get; set; } = 0;
    public int       GatesFailed     { get; set; } = 0;
    public string    GateResults     { get; set; } = "[]"; // JSON — serialized GateResult[]
    public Product   Product         { get; set; } = null!;
}
