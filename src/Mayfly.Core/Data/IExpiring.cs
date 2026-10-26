namespace Mayfly.Core.Data;

public interface IExpiring
{
    DateTimeOffset? ExpiresAt { get; }
}
