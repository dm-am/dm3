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
    private readonly IRegistrationRepository registrationRepository;
    private readonly IUsernameChangeRepository usernameChangeRepository;
    private readonly IUsernameHistoryRepository usernameHistoryRepository;
    private readonly AvailabilityService service;

    public AvailabilityServiceShould()
    {
        registrationRepository = Mock<IRegistrationRepository>();
        usernameChangeRepository = Mock<IUsernameChangeRepository>();
        usernameHistoryRepository = Mock<IUsernameHistoryRepository>();

        service = new AvailabilityService(
            registrationRepository,
            usernameChangeRepository,
            usernameHistoryRepository);
    }

    [Fact]
    public async Task ReturnAvailableForUnusedEmail()
    {
        var email = "newuser@example.com";
        registrationRepository.EmailFreeForNewRegistration(email, Arg.Any<CancellationToken>()).Returns(true);
        registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>())
            .Returns((PendingRegistration?)null);

        var result = await service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeTrue();
    }

    // Deactivated accounts included: they keep their address, and the probe has to say so.
    [Fact]
    public async Task ReturnUnavailableWhenEmailIsHeldByAnyAccount()
    {
        var email = "existing@example.com";
        registrationRepository.EmailFreeForNewRegistration(email, Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(EmailUnavailableReason.Taken);
    }

    [Fact]
    public async Task ReturnUnavailableWhenEmailHasPendingRegistration()
    {
        var email = "pending@example.com";
        registrationRepository.EmailFreeForNewRegistration(email, Arg.Any<CancellationToken>()).Returns(true);
        registrationRepository.FindPendingByEmail(email, Arg.Any<CancellationToken>())
            .Returns(new PendingRegistration { Email = email });

        var result = await service.CheckEmailAvailability(email);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(EmailUnavailableReason.PendingActivation);
    }

    [Fact]
    public async Task ReturnAvailableForValidUsername()
    {
        var username = "ValidUsername";
        usernameChangeRepository.IsUsernameAvailable(username, null, Arg.Any<CancellationToken>()).Returns(true);
        usernameHistoryRepository.IsUsernameReserved(username, Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("user<script>")]
    public async Task ReturnInvalidFormatForAMalformedUsername(string username)
    {
        var result = await service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.InvalidFormat);
    }

    [Fact]
    public async Task ReturnTakenForExistingUsername()
    {
        var username = "ExistingUser";
        usernameChangeRepository.IsUsernameAvailable(username, null, Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.Taken);
    }

    // A name a former user left behind answers as taken, not as reserved: the guest surface gives
    // out one bit, and telling the two apart tells a caller somebody was here and left
    // (SECURITY.md).
    [Fact]
    public async Task AnswerTakenForAHistoricalUsername()
    {
        var username = "FormerUser";
        usernameChangeRepository.IsUsernameAvailable(username, null, Arg.Any<CancellationToken>()).Returns(true);
        usernameHistoryRepository.IsUsernameReserved(username, Arg.Any<CancellationToken>()).Returns(true);

        var result = await service.CheckUsernameAvailability(username);

        result.IsAvailable.Should().BeFalse();
        result.Reason.Should().Be(UsernameUnavailableReason.Taken);
    }
}
