using System.Net;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Identity;

/// <summary>
/// Reads of the current identity
/// </summary>
public static class IdentityProviderExtensions
{
    /// <summary>
    /// The signed-in user, or 401 for an anonymous caller
    /// </summary>
    /// <exception cref="HttpException">Unauthorized, when nobody is signed in</exception>
    public static AuthenticatedUser RequireAuthenticated(this IIdentityProvider identityProvider)
    {
        var user = identityProvider.Current.User;
        return user.IsAuthenticated
            ? user
            : throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
    }
}
