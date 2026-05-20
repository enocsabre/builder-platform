namespace BuilderPlatform.Domain.Entities;

public class DeployRun
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public Guid      ProductId   { get; set; }
    public string    Status      { get; set; } = "running";   // running | passed | failed
    public DateTime  StartedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt  { get; set; }
    public string?   Logs        { get; set; }
    public string?   Errors      { get; set; }
    public string?   DeployUrl   { get; set; }
    public string?   CommitHash  { get; set; }
    public string?   Branch      { get; set; }
    public string    GateResults { get; set; } = "[]";
    public Product   Product     { get; set; } = null!;
}
