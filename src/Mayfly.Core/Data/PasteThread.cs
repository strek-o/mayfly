namespace Mayfly.Core.Data;

public class PasteThread
{
    public Guid Id { get; set; }

    public Guid PasteId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<ThreadEntry> Entries { get; } = [];
}
