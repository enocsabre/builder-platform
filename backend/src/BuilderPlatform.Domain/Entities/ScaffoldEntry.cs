namespace BuilderPlatform.Domain.Entities;

public class ScaffoldEntry
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public Guid     ProductId    { get; set; }
    public string   RelativePath { get; set; } = string.Empty;
    public string   EntryType    { get; set; } = "file";
    public string?  Language     { get; set; }
    public int      SortOrder    { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
