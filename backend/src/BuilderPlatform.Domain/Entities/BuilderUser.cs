namespace BuilderPlatform.Domain.Entities;

public class BuilderUser
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   Email        { get; set; } = string.Empty;
    public string   PasswordHash { get; set; } = string.Empty;
    public string?  Name         { get; set; }
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
}
