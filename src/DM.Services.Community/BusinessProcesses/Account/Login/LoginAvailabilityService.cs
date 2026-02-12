using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.Login;

/// <summary>
/// Service for checking login availability
/// </summary>
internal partial class LoginAvailabilityService : ILoginAvailabilityService
{
    private readonly DmDbContext _dbContext;

    // Forbidden: control chars, HTML/URL unsafe, quotes, brackets, special chars, zero-width
    // Whitespace: not at start/end, not consecutive
    // See: docs/architecture/USERNAME_POLICY.md
    [GeneratedRegex(@"^(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\/@?#%&\[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$")]
    private static partial Regex LoginValidationRegex();

    public LoginAvailabilityService(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoginAvailabilityResult> CheckAvailability(string login, CancellationToken cancellationToken = default)
    {
        // Validate format first
        if (string.IsNullOrWhiteSpace(login))
            return LoginAvailabilityResult.InvalidFormat();

        login = login.Trim();

        if (!LoginValidationRegex().IsMatch(login))
            return LoginAvailabilityResult.InvalidFormat();

        // Check if taken by existing user
        var existsInUsers = await _dbContext.Users
            .AnyAsync(u => EF.Functions.ILike(u.Login, login), cancellationToken);

        if (existsInUsers)
            return LoginAvailabilityResult.Taken();

        // Check if reserved (used by former user)
        var existsInHistory = await _dbContext.LoginHistories
            .AnyAsync(h => EF.Functions.ILike(h.OldLogin, login), cancellationToken);

        if (existsInHistory)
            return LoginAvailabilityResult.Reserved();

        return LoginAvailabilityResult.Available();
    }
}
