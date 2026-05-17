namespace BuilderPlatform.Domain.Entities;

public class FileRevision
{
    public Guid     Id             { get; set; } = Guid.NewGuid();
    public Guid     ProductId      { get; set; }
    public string   RelativePath   { get; set; } = string.Empty;
    public string   PatchType      { get; set; } = string.Empty;
    public string   Reason         { get; set; } = string.Empty;
    public string?  BeforeContent  { get; set; }
    public string   AfterContent   { get; set; } = string.Empty;
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
