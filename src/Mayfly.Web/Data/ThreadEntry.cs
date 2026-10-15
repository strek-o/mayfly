namespace Mayfly.Web.Data;

public class ThreadEntry
{
    public Guid Id { get; set; }

    public Guid ThreadId { get; set; }

    public required string Content { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
