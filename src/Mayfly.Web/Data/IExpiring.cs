namespace Mayfly.Web.Data;

public interface IExpiring
{
    DateTimeOffset? ExpiresAt { get; }
}
