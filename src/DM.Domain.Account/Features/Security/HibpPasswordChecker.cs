using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Checks passwords against HaveIBeenPwned database using k-anonymity model.
/// Only the first 5 characters of the SHA-1 hash are sent to the API.
/// </summary>
/// <remarks>
/// NIST SP 800-63B-4 requires checking passwords against known compromised lists.
/// This implementation uses the HIBP Pwned Passwords API with privacy protection.
/// </remarks>
public class HibpPasswordChecker : ICompromisedPasswordChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HibpPasswordChecker> _logger;
    private const string HibpApiUrl = "https://api.pwnedpasswords.com/range/";

    /// <summary>
    /// Creates a new HIBP password checker
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls</param>
    /// <param name="logger">Logger instance</param>
    public HibpPasswordChecker(
        HttpClient httpClient,
        ILogger<HibpPasswordChecker> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsCompromisedAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            var sha1Hash = ComputeSha1Hash(password);
            var prefix = sha1Hash[..5];
            var suffix = sha1Hash[5..];

            var response = await _httpClient.GetAsync($"{HibpApiUrl}{prefix}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HIBP API returned {StatusCode}. Allowing password (fail-open)",
                    response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Check if our suffix is in the response
            var isCompromised = lines.Any(line =>
            {
                var parts = line.Split(':');
                return parts.Length > 0 &&
                       parts[0].Trim().Equals(suffix, StringComparison.OrdinalIgnoreCase);
            });

            if (isCompromised)
            {
                _logger.LogInformation("Password found in HIBP database (compromised)");
            }

            return isCompromised;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HIBP API unavailable. Allowing password (fail-open)");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "HIBP API timeout. Allowing password (fail-open)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking HIBP. Allowing password (fail-open)");
            return false;
        }
    }

    private static string ComputeSha1Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA1.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
