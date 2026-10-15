namespace Mayfly.Web.Data;

public class Paste
{
    public Guid Id { get; set; }

    public required string OwnerId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<PasteThread> Threads { get; } = [];
}
