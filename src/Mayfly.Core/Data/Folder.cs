namespace Mayfly.Core.Data;

public class Folder
{
    public Guid Id { get; set; }

    public required string OwnerId { get; set; }

    public Guid? ParentId { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int? GridColumn { get; set; }

    public int? GridRow { get; set; }
}
