using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Users;

namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Service for checking email and username availability
/// </summary>
internal class AvailabilityService : IAvailabilityService
{
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IUsernameChangeRepository _usernameChangeRepository;
    private readonly IUsernameHistoryRepository _usernameHistoryRepository;

    public AvailabilityService(
        IRegistrationRepository registrationRepository,
        IUsernameChangeRepository usernameChangeRepository,
        IUsernameHistoryRepository usernameHistoryRepository)
    {
        _registrationRepository = registrationRepository;
        _usernameChangeRepository = usernameChangeRepository;
        _usernameHistoryRepository = usernameHistoryRepository;
    }

    /// <inheritdoc />
    public async Task<EmailAvailabilityResult> CheckEmailAvailability(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // The answer the registration validator gives. A removed account holds its
        // address like a live one and answers like one (SECURITY.md).
        if (!await _registrationRepository.EmailFreeForNewRegistration(normalizedEmail, cancellationToken))
        {
            return EmailAvailabilityResult.Unavailable(EmailUnavailableReason.Taken);
        }

        var pending = await _registrationRepository.FindPendingByEmail(normalizedEmail, cancellationToken);
        if (pending != null)
        {
            return EmailAvailabilityResult.Unavailable(EmailUnavailableReason.PendingActivation);
        }

        return EmailAvailabilityResult.Available();
    }

    /// <inheritdoc />
    public async Task<UsernameAvailabilityResult> CheckUsernameAvailability(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return UsernameAvailabilityResult.InvalidFormat();

        username = username.Trim();

        if (!UsernamePolicy.IsValid(username))
            return UsernameAvailabilityResult.InvalidFormat();

        var isAvailableInUsers = await _usernameChangeRepository.IsUsernameAvailable(username, ct: cancellationToken);
        if (!isAvailableInUsers)
            return UsernameAvailabilityResult.Taken();

        // A reserved name answers "taken" like one in use (SECURITY.md).
        var isReserved = await _usernameHistoryRepository.IsUsernameReserved(username, cancellationToken);
        if (isReserved)
            return UsernameAvailabilityResult.Taken();

        return UsernameAvailabilityResult.Available();
    }
}
