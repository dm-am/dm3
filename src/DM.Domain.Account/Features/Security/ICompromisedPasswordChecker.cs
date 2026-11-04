using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Service for checking if passwords have been compromised in data breaches
/// </summary>
public interface ICompromisedPasswordChecker
{
    /// <summary>
    /// Check if password has been found in known data breaches
    /// </summary>
    /// <returns>True if password is compromised, false otherwise</returns>
    /// <remarks>
    /// Uses k-anonymity model - only first 5 characters of SHA-1 hash are sent to the API.
    /// If the service is unavailable, returns false (fail-open for availability).
    /// </remarks>
    Task<bool> IsCompromisedAsync(string password);
}

internal static class CompromisedPasswordCheckerExtensions
{
    /// <summary>
    /// Refuse a password found in a breach, as a validation error of the field it was typed in
    /// </summary>
    public static async Task ThrowIfCompromised(
        this ICompromisedPasswordChecker checker, string password, string field)
    {
        if (await checker.IsCompromisedAsync(password))
        {
            throw new HttpBadRequestException(new Dictionary<string, string>
            {
                [field] = RefusalMessage.PasswordBreached
            });
        }
    }
}
