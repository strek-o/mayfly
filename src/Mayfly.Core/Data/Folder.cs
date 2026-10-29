namespace Mayfly.Core.Data;

public class Folder
{
    public Guid Id { get; set; }

    public required string OwnerId { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
