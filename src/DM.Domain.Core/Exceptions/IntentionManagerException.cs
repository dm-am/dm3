using System.Net;
using System.Reflection;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Specific exception when user tries to perform unauthorized action
/// </summary>
public class IntentionManagerException : HttpException
{
    public IntentionManagerException(IAuthorizationSubject user, Enum intention, object? target)
        : base(HttpStatusCode.Forbidden, GenerateMessage(user, intention, target))
    {
    }

    public IntentionManagerException(IAuthorizationSubject user, Enum intention)
        : base(HttpStatusCode.Forbidden, GenerateMessage(user, intention))
    {
    }

    /// <summary>
    /// The message identifies the target, it does not reproduce it.
    /// </summary>
    /// <remarks>
    /// The message becomes the response title, so it must not carry the refused object.
    /// </remarks>
    private static string GenerateMessage(IAuthorizationSubject user, Enum intention, object? target = null) =>
        $"User {user.UserId} is not allowed to perform {intention} action" +
        (target == null ? string.Empty : $" on {Describe(target)}");

    /// <summary>Type name, plus the identifier when the target carries one.</summary>
    private static string Describe(object target)
    {
        var type = target.GetType();
        var id = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(target)
                 ?? type.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);

        return id is null ? type.Name : $"{type.Name} {id}";
    }
}
