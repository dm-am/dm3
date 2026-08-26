using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.UsernameChange;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Availability;

public class AvailabilityServiceShould : UnitTestBase
{
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IUsernameChangeRepository _usernameChangeRepository;
    private readonly IUsernameHistoryRepository _usernameHistoryRepository;
    private readonly AvailabilityService _service;

    public AvailabilityServiceShould()
    {
        _registrationRepository = Mock<IRegistrationRepository>();
        _usernameChangeRepository = Mock<IUsernameChangeRepository>();
        _usernameHistoryRepository = Mock<IUsernameHistoryRepository>();

        _service = new AvailabilityService(
            _registrationRepository,
            _usernameChangeRepository,
            _usernameHistoryRepository);
    }

    [Fact]
    public async Task ReturnAvailableForUnusedEmail()
    {
        var email = "newuser@example.com";
        _registrationRepository.EmailFreeForNewRegistration(email, Arg.Any<CancellationToken>()).Returns(true);
        _registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>())
            .Returns((PendingRegistration?)null);

        var result = await _service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeTrue();
    }

    /// <summary>
    /// Deactivated accounts included: they keep their address, and the probe has to say so.
    /// </summary>
    [Fact]
    public async Task ReturnUnavailableWhenEmailIsHeldByAnyAccount()
    {
        var email = "existing@example.com";
        _registrationRepository.EmailFreeForNewRegistration(email, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(EmailUnavailableReason.Taken);
    }

    [Fact]
    public async Task ReturnUnavailableWhenEmailHasPendingRegistration()
    {
        var email = "pending@example.com";
        _registrationRepository.EmailFreeForNewRegistration(email, Arg.Any<CancellationToken>()).Returns(true);
        _registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>())
            .Returns(new PendingRegistration { Email = email });

        var result = await _service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(EmailUnavailableReason.PendingActivation);
    }

    [Fact]
    public async Task ReturnAvailableForValidUsername()
    {
        var username = "ValidUsername";
        _usernameChangeRepository.IsUsernameAvailable(username, null, Arg.Any<CancellationToken>()).Returns(true);
        _usernameHistoryRepository.IsUsernameReserved(username, Arg.Any<CancellationToken>()).Returns(false);

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
        _usernameChangeRepository.IsUsernameAvailable(username, null, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.Taken);
    }

    [Fact]
    public async Task ReturnReservedForHistoricalUsername()
    {
        var username = "FormerUser";
        _usernameChangeRepository.IsUsernameAvailable(username, null, Arg.Any<CancellationToken>()).Returns(true);
        _usernameHistoryRepository.IsUsernameReserved(username, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.Reserved);
    }
}
