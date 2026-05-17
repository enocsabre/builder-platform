namespace BuilderPlatform.Domain.Entities;

public class ScaffoldChange
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid     ProductId   { get; set; }
    public string   ChangeType  { get; set; } = "created"; // created | skipped
    public string   TargetPath  { get; set; } = string.Empty;
    public string   ModuleLabel { get; set; } = string.Empty;
    public string   Layer       { get; set; } = "backend"; // backend | frontend
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
