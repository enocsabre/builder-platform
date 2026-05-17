namespace BuilderPlatform.Domain.Entities;

public class ChatMessage
{
    public Guid      Id              { get; set; } = Guid.NewGuid();
    public Guid      ProductId       { get; set; }
    public MessageRole Role          { get; set; }
    public string    Content         { get; set; } = string.Empty;
    public string?   DetectedIntent  { get; set; }
    public double?   Confidence      { get; set; }
    public DateTime  CreatedAt       { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}

public enum MessageRole
{
    User,
    Runtime,
    System,
}
