using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

/// <summary>
/// A login from an address the account has never used is suspicious, and the
/// login being judged is not evidence about itself.
/// </summary>
/// <remarks>
/// The trail is written before the detector is asked. Counting the current entry
/// made every address known to itself, so the answer was false for every login
/// there has ever been: no letter from a new device, no journal entry, a whole
/// control dead in a way nothing reports.
///
/// The second half is the same mistake one step out. A failed attempt records
/// the address of whoever was guessing, so treating failures as history lets one
/// wrong password launder the next one into "known", which is exactly the
/// sequence the letter exists to catch.
/// </remarks>
public class SuspiciousLoginDetectorShould : UnitTestBase
{
    private const string KnownAddress = "203.0.113.7";
    private const string NewAddress = "198.51.100.9";

    private readonly Guid _userId = Guid.NewGuid();
    private readonly ISecurityAuditRepository _auditService;
    private readonly SuspiciousLoginDetector _detector;

    public SuspiciousLoginDetectorShould()
    {
        _auditService = Mock<ISecurityAuditRepository>();
        _detector = new SuspiciousLoginDetector(_auditService);
    }

    [Fact]
    public async Task CallANewAddressSuspiciousThoughTheTrailAlreadyHoldsIt()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, NewAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, NewAddress, "agent");

        suspicious.Should().BeTrue();
    }

    [Fact]
    public async Task CallAFamiliarAddressOrdinary()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, KnownAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, KnownAddress, "agent");

        suspicious.Should().BeFalse();
    }

    /// <summary>
    /// A failed attempt is not proof the address is the owner's.
    /// </summary>
    /// <remarks>
    /// The detector asks for successes and nothing else, so the guessing never
    /// reaches it: what is asserted here is that the address the guesses came
    /// from is still unknown when a login finally succeeds from it.
    /// </remarks>
    [Fact]
    public async Task NotTakeAFailedAttemptAsProofTheAddressIsTheOwners()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, NewAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, NewAddress, "agent");

        suspicious.Should().BeTrue();
    }

    /// <summary>
    /// A run of wrong passwords does not erase where the owner logs in from.
    /// </summary>
    /// <remarks>
    /// This is the sequence the detector exists for — guess, guess, guess, then
    /// succeed — and it was the one it could not see. The window was twenty
    /// entries of any kind, filtered to successes afterwards, so nineteen
    /// failures pushed every previous success out of it and the login that
    /// followed met an empty history and was called ordinary.
    /// </remarks>
    [Fact]
    public async Task StillKnowTheOwnersAddressAfterALongRunOfFailedAttempts()
    {
        // What the repository returns when it is asked for successes: the run of
        // failures in between is filtered by the query, not by the caller.
        Trail(
            Entry(SecurityEventType.LoginSuccess, NewAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, NewAddress, "agent");

        suspicious.Should().BeTrue("the guessing did not make the new address the owner's");
        // The mixed trail spends the window on entries this cannot use, so there is
        // no second look for more of them.
        await _auditService.DidNotReceive().GetByTypesAsync(
            Arg.Any<Guid>(), SecurityEventCategories.Login, Arg.Any<int>());
    }

    [Fact]
    public async Task CallTheFirstLoginOfAnAccountOrdinary()
    {
        Trail(Entry(SecurityEventType.LoginSuccess, NewAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, NewAddress, "agent");

        suspicious.Should().BeFalse();
    }

    /// <param name="newestFirst">The trail as the repository returns it</param>
    private void Trail(params SecurityAuditEntry[] newestFirst) =>
        _auditService.GetByTypesAsync(
                _userId, SecurityEventCategories.SuccessfulLogins, Arg.Any<int>()).Returns(newestFirst);

    private static SecurityAuditEntry Entry(SecurityEventType type, string address) =>
        new() { EventType = type, IpAddress = address };

    private static SecurityAuditEntry Entry(SecurityEventType type, string address, string device) =>
        new() { EventType = type, IpAddress = address, DeviceInfo = device };

    /// <summary>
    /// A reconnection is not a new place.
    /// </summary>
    /// <remarks>
    /// Compared exactly, the address of a residential connection changes on the
    /// provider's schedule and the address of a phone changes every time it leaves
    /// the house, so half the logins of an ordinary reader were called suspicious.
    /// A warning that arrives on ordinary days is one nobody reads on the day it
    /// matters, which is the only day it exists for.
    /// </remarks>
    [Fact]
    public async Task TreatTheSameNetworkAsTheSamePlace()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, "203.0.113.42"),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, "203.0.113.180", null);

        suspicious.Should().BeFalse("the provider renumbered the connection, nobody moved");
    }

    [Fact]
    public async Task StillCallAnotherNetworkSuspicious()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, "203.0.113.42"),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, NewAddress, null);

        suspicious.Should().BeTrue();
    }

    /// <summary>
    /// The device was taken as an argument and never read.
    /// </summary>
    /// <remarks>
    /// A session opened from the owner's own network on a machine that has never
    /// been seen — somebody sitting at a browser that is not theirs — was
    /// indistinguishable from the owner opening their own laptop.
    /// </remarks>
    [Fact]
    public async Task CallAnUnseenDeviceSuspiciousFromAKnownNetwork()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, KnownAddress, "Chrome on Windows"),
            Entry(SecurityEventType.LoginSuccess, KnownAddress, "Chrome on Windows"));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, KnownAddress,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 " +
            "(KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");

        suspicious.Should().BeTrue();
    }

    [Fact]
    public async Task SayNothingWhenTheSameDeviceReturnsToTheSameNetwork()
    {
        var chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                     "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var device = DM.Domain.Core.Parsing.UserAgentParser.Parse(chrome);

        Trail(
            Entry(SecurityEventType.LoginSuccess, KnownAddress, device),
            Entry(SecurityEventType.LoginSuccess, KnownAddress, device));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, KnownAddress, chrome);

        suspicious.Should().BeFalse("the same browser on the same connection is the owner");
    }
}
