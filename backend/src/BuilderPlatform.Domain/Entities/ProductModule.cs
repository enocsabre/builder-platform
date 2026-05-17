namespace BuilderPlatform.Domain.Entities;

public class ProductModule
{
    public Guid     Id             { get; set; } = Guid.NewGuid();
    public Guid     ProductId      { get; set; }
    public string   ModuleName     { get; set; } = string.Empty;
    public string   EntityName     { get; set; } = string.Empty;
    public string   RoutePath      { get; set; } = string.Empty;
    public string   ControllerName { get; set; } = string.Empty;
    public string   Layer          { get; set; } = "full-stack"; // backend | frontend | full-stack
    public string   Source         { get; set; } = "scaffold";   // scaffold | delta
    public bool     IsActive       { get; set; } = true;
    public DateTime DetectedAt     { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
