namespace Ledger.Api.Auth;

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser currentUser)
    {
        return currentUser.UserId
            ?? throw new InvalidOperationException("The request is not associated with a user.");
    }
}
