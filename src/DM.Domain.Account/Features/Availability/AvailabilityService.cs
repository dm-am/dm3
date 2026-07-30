using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.UsernameChange;

namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Service for checking email and username availability
/// </summary>
internal partial class AvailabilityService : IAvailabilityService
{
    private readonly IEmailLookupRepository _emailLookupRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IUsernameChangeRepository _usernameChangeRepository;
    private readonly IUsernameHistoryRepository _usernameHistoryRepository;

    // Forbidden: control chars, HTML/URL unsafe, quotes, brackets, special chars, zero-width
    // Whitespace: not at start/end, not consecutive
    // See: docs/conventions/USERNAME_POLICY.md
    [GeneratedRegex(@"^(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\/@?#%&\[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$")]
    private static partial Regex UsernameValidationRegex();

    public AvailabilityService(
        IEmailLookupRepository emailLookupRepository,
        IRegistrationRepository registrationRepository,
        IUsernameChangeRepository usernameChangeRepository,
        IUsernameHistoryRepository usernameHistoryRepository)
    {
        _emailLookupRepository = emailLookupRepository;
        _registrationRepository = registrationRepository;
        _usernameChangeRepository = usernameChangeRepository;
        _usernameHistoryRepository = usernameHistoryRepository;
    }

    /// <inheritdoc />
    public async Task<EmailAvailabilityResult> CheckEmailAvailability(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Check if email is taken by active user
        var user = await _emailLookupRepository.GetUserByEmail(normalizedEmail);
        if (user != null)
        {
            return EmailAvailabilityResult.Unavailable(EmailUnavailableReason.Taken);
        }

        // Check if email has pending registration
        var pending = await _registrationRepository.FindPendingByEmail(normalizedEmail, cancellationToken);
        if (pending != null)
        {
            return EmailAvailabilityResult.Unavailable(EmailUnavailableReason.PendingActivation);
        }

        // Email is available
        return EmailAvailabilityResult.Available();
    }

    /// <inheritdoc />
    public async Task<UsernameAvailabilityResult> CheckUsernameAvailability(string username, CancellationToken cancellationToken = default)
    {
        // Validate format first
        if (string.IsNullOrWhiteSpace(username))
            return UsernameAvailabilityResult.InvalidFormat();

        username = username.Trim();

        if (!UsernameValidationRegex().IsMatch(username))
            return UsernameAvailabilityResult.InvalidFormat();

        // Check if taken by existing user
        var isAvailableInUsers = await _usernameChangeRepository.IsUsernameAvailable(username, ct: cancellationToken);
        if (!isAvailableInUsers)
            return UsernameAvailabilityResult.Taken();

        // Check if reserved (used by former user)
        var isReserved = await _usernameHistoryRepository.IsUsernameReserved(username, cancellationToken);
        if (isReserved)
            return UsernameAvailabilityResult.Reserved();

        return UsernameAvailabilityResult.Available();
    }
}
