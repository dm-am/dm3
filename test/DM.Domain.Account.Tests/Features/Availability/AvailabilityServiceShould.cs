using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.UsernameChange;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Availability;

public class AvailabilityServiceShould : UnitTestBase
{
    private readonly Mock<IEmailLookupRepository> _emailLookupRepository;
    private readonly Mock<IRegistrationRepository> _registrationRepository;
    private readonly Mock<IUsernameChangeRepository> _usernameChangeRepository;
    private readonly Mock<IUsernameHistoryRepository> _usernameHistoryRepository;
    private readonly AvailabilityService _service;

    public AvailabilityServiceShould()
    {
        _emailLookupRepository = Mock<IEmailLookupRepository>();
        _registrationRepository = Mock<IRegistrationRepository>();
        _usernameChangeRepository = Mock<IUsernameChangeRepository>();
        _usernameHistoryRepository = Mock<IUsernameHistoryRepository>();

        _service = new AvailabilityService(
            _emailLookupRepository.Object,
            _registrationRepository.Object,
            _usernameChangeRepository.Object,
            _usernameHistoryRepository.Object);
    }

    [Fact]
    public async Task ReturnAvailableForUnusedEmail()
    {
        var email = "newuser@example.com";
        _emailLookupRepository.Setup(r => r.GetUserByEmail(email, It.IsAny<CancellationToken>())).ReturnsAsync((EmailLookupInfo?)null);
        _registrationRepository.Setup(r => r.FindPendingByEmail(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        var result = await _service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnUnavailableWhenEmailIsTakenByActiveUser()
    {
        var email = "existing@example.com";
        _emailLookupRepository.Setup(r => r.GetUserByEmail(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailLookupInfo { Email = email });

        var result = await _service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(EmailUnavailableReason.Taken);
    }

    [Fact]
    public async Task ReturnUnavailableWhenEmailHasPendingRegistration()
    {
        var email = "pending@example.com";
        _emailLookupRepository.Setup(r => r.GetUserByEmail(email, It.IsAny<CancellationToken>())).ReturnsAsync((EmailLookupInfo?)null);
        _registrationRepository.Setup(r => r.FindPendingByEmail(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingRegistration { Email = email });

        var result = await _service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(EmailUnavailableReason.PendingActivation);
    }

    [Fact]
    public async Task ReturnAvailableForValidUsername()
    {
        var username = "ValidUsername";
        _usernameChangeRepository.Setup(r => r.IsUsernameAvailable(username, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _usernameHistoryRepository.Setup(r => r.IsUsernameReserved(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnInvalidFormatForEmptyUsername()
    {
        var result = await _service.CheckUsernameAvailability("");

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.InvalidFormat);
    }

    [Fact]
    public async Task ReturnInvalidFormatForUsernameWithInvalidCharacters()
    {
        var result = await _service.CheckUsernameAvailability("user<script>");

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.InvalidFormat);
    }

    [Fact]
    public async Task ReturnTakenForExistingUsername()
    {
        var username = "ExistingUser";
        _usernameChangeRepository.Setup(r => r.IsUsernameAvailable(username, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.Taken);
    }

    [Fact]
    public async Task ReturnReservedForHistoricalUsername()
    {
        var username = "FormerUser";
        _usernameChangeRepository.Setup(r => r.IsUsernameAvailable(username, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _usernameHistoryRepository.Setup(r => r.IsUsernameReserved(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.Reserved);
    }
}
