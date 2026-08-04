using System;
using System.Net;
using System.Reflection;
using System.Text;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Specific exception when user tries to perform unauthorized action
/// </summary>
public class IntentionManagerException : HttpException
{
    /// <inheritdoc />
    public IntentionManagerException(IAuthorizationSubject user, Enum intention, object? target)
        : base(HttpStatusCode.Forbidden, GenerateMessage(user, intention, target))
    {
    }

    /// <inheritdoc />
    public IntentionManagerException(IAuthorizationSubject user, Enum intention)
        : base(HttpStatusCode.Forbidden, GenerateMessage(user, intention))
    {
    }

    /// <summary>
    /// The message identifies the target, it does not reproduce it.
    /// </summary>
    /// <remarks>
    /// This used to append JsonSerializer.Serialize(target), and the message is
    /// what the problem-details factory puts in the response title — so a refused
    /// request answered with the entire object it had refused. Measured against
    /// the running stack: an anonymous GET of a blog whose draft is private came
    /// back 403 carrying the blog DTO, the draft description and the author's
    /// GeneralUser — email, real name, city, ratings. The same string went to the
    /// log at Warning level.
    ///
    /// Every authorization refusal in the project goes through this constructor,
    /// so the type name plus the identifier is what a log needs to answer "who
    /// was refused what", and it is all anybody gets.
    /// </remarks>
    private static string GenerateMessage(IAuthorizationSubject user, Enum intention, object? target = null)
    {
        var result = new StringBuilder($"User {user.UserId} is not allowed to perform {intention} action");
        if (target == null)
        {
            return result.ToString();
        }

        result.Append($" on {Describe(target)}");
        return result.ToString();
    }

    /// <summary>Type name, plus the identifier when the target carries one.</summary>
    private static string Describe(object target)
    {
        var type = target.GetType();
        var id = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(target)
                 ?? type.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);

        return id is null ? type.Name : $"{type.Name} {id}";
    }
}
