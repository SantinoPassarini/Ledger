namespace Ledger.Api.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
