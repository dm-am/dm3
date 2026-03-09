using System;
using System.Net;
using System.Text;
using System.Text.Json;
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

    private static string GenerateMessage(IAuthorizationSubject user, Enum intention, object? target = null)
    {
        var result = new StringBuilder($"User {user.UserId} is not allowed to perform {intention} action");
        if (target == null)
        {
            return result.ToString();
        }

        result.Append($" on {JsonSerializer.Serialize(target)}");
        return result.ToString();
    }
}
