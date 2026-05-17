namespace BuilderPlatform.Domain.Entities;

public class ProductMemory
{
    public Guid     Id        { get; set; } = Guid.NewGuid();
    public Guid     ProductId { get; set; }
    public string   Key       { get; set; } = string.Empty;
    public string   Value     { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
