using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

/// <summary>
/// An expired lockout is lifted for the pair that waited it out, and for nobody
/// else.
/// </summary>
/// <remarks>
/// Counters are keyed on (account, address) so that fifteen deliberately wrong
/// passwords cannot lock a moderator out from everywhere. Lifting an expired
/// lockout by email threw that away the moment a window anywhere ran out: the
/// records of every other address went with it, lockout starts and accumulated
/// delay included, and a spread-out attempt collected a fresh fifteen tries per
/// address. Clearing the whole account is what a correct password earns, and it
/// belongs on that path alone.
/// </remarks>
public class LoginAttemptTrackerShould : UnitTestBase
{
    private readonly DateTimeOffset _now = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
    private readonly LoginAttemptOrigin _origin = new("test@example.com", "203.0.113.7");
    private readonly ILoginAttemptRepository _repository;
    private readonly LoginAttemptTracker _tracker;

    public LoginAttemptTrackerShould()
    {
        _repository = Mock<ILoginAttemptRepository>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(_now);

        _tracker = new LoginAttemptTracker(
            _repository,
            dateTimeProvider,
            Options.Create(new AuthenticationConfiguration
            {
                AccountLockoutDurationMinutes = 30,
                AccountLockoutThreshold = 15
            }));
    }

    [Fact]
    public async Task LiftAnExpiredLockoutForItsOwnAddressOnly()
    {
        _repository.GetLockoutStart(_origin).Returns(_now.UtcDateTime.AddHours(-1));

        var locked = await _tracker.IsAccountLocked(_origin);

        locked.Should().BeFalse();
        await _repository.Received(1).ResetAttempts(_origin);
        await _repository.DidNotReceive().ResetAttempts(Arg.Any<string>());
    }

    [Fact]
    public async Task KeepAnUnexpiredLockoutAndClearNothing()
    {
        _repository.GetLockoutStart(_origin).Returns(_now.UtcDateTime.AddMinutes(-1));

        var locked = await _tracker.IsAccountLocked(_origin);

        locked.Should().BeTrue();
        await _repository.DidNotReceive().ResetAttempts(Arg.Any<LoginAttemptOrigin>());
        await _repository.DidNotReceive().ResetAttempts(Arg.Any<string>());
    }
}
