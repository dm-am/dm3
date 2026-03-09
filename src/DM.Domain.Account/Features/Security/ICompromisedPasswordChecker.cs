using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Service for checking if passwords have been compromised in data breaches
/// </summary>
public interface ICompromisedPasswordChecker
{
    /// <summary>
    /// Check if password has been found in known data breaches
    /// </summary>
    /// <param name="password">Password to check</param>
    /// <returns>True if password is compromised, false otherwise</returns>
    /// <remarks>
    /// Uses k-anonymity model - only first 5 characters of SHA-1 hash are sent to the API.
    /// If the service is unavailable, returns false (fail-open for availability).
    /// </remarks>
    Task<bool> IsCompromisedAsync(string password);
}
