namespace Mayfly.Core.Data;

public class Folder : IExpiring
{
    public Guid Id { get; set; }

    public required string OwnerId { get; set; }

    public Guid? ParentId { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public int? GridColumn { get; set; }

    public int? GridRow { get; set; }
}
