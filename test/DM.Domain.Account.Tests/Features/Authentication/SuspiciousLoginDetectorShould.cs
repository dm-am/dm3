using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Parsing;
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
/// The trail is written before the detector is asked, and a failed attempt records whoever was guessing.
/// </remarks>
public class SuspiciousLoginDetectorShould : UnitTestBase
{
    private const string KnownAddress = "203.0.113.7";
    private const string NewAddress = "198.51.100.9";

    private readonly Guid userId = Guid.NewGuid();
    private readonly ISecurityAuditRepository auditService;
    private readonly SuspiciousLoginDetector detector;

    public SuspiciousLoginDetectorShould()
    {
        auditService = Mock<ISecurityAuditRepository>();
        detector = new SuspiciousLoginDetector(auditService);
    }

    /// <summary>
    /// The detector asks for successes and nothing else, so a run of failed
    /// guesses neither proves the new address nor erases the owner's.
    /// </summary>
    [Fact]
    public async Task CallANewAddressSuspiciousThoughTheTrailAlreadyHoldsIt()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, NewAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await detector.IsSuspiciousAsync(userId, NewAddress, "agent");

        suspicious.Should().BeTrue();
        await auditService.DidNotReceive().GetByTypesAsync(
            Arg.Any<Guid>(), SecurityEventCategories.Login, Arg.Any<int>());
    }

    [Fact]
    public async Task CallAFamiliarAddressOrdinary()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, KnownAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await detector.IsSuspiciousAsync(userId, KnownAddress, "agent");

        suspicious.Should().BeFalse();
    }

    [Fact]
    public async Task CallTheFirstLoginOfAnAccountOrdinary()
    {
        Trail(Entry(SecurityEventType.LoginSuccess, NewAddress));

        var suspicious = await detector.IsSuspiciousAsync(userId, NewAddress, "agent");

        suspicious.Should().BeFalse();
    }

    /// <param name="newestFirst">The trail as the repository returns it</param>
    private void Trail(params SecurityAuditEntry[] newestFirst) =>
        auditService.GetByTypesAsync(
                userId, SecurityEventCategories.SuccessfulLogins, Arg.Any<int>()).Returns(newestFirst);

    private static SecurityAuditEntry Entry(SecurityEventType type, string address) =>
        new() { EventType = type, IpAddress = address };

    private static SecurityAuditEntry Entry(SecurityEventType type, string address, string device) =>
        new() { EventType = type, IpAddress = address, DeviceInfo = device };

    /// <summary>
    /// A reconnection is not a new place.
    /// </summary>
    /// <remarks>
    /// A home or phone address changes on the provider's schedule, and a warning on ordinary days is one nobody reads.
    /// </remarks>
    [Fact]
    public async Task TreatTheSameNetworkAsTheSamePlace()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, "203.0.113.42"),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await detector.IsSuspiciousAsync(userId, "203.0.113.180", null);

        suspicious.Should().BeFalse("the provider renumbered the connection, nobody moved");
    }

    [Fact]
    public async Task StillCallAnotherNetworkSuspicious()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, "203.0.113.42"),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await detector.IsSuspiciousAsync(userId, NewAddress, null);

        suspicious.Should().BeTrue();
    }

    /// <summary>
    /// A machine never seen on the owner's own network is a new place too.
    /// </summary>
    [Fact]
    public async Task CallAnUnseenDeviceSuspiciousFromAKnownNetwork()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, KnownAddress, "Chrome on Windows"),
            Entry(SecurityEventType.LoginSuccess, KnownAddress, "Chrome on Windows"));

        var suspicious = await detector.IsSuspiciousAsync(userId, KnownAddress,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 " +
            "(KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");

        suspicious.Should().BeTrue();
    }

    [Fact]
    public async Task SayNothingWhenTheSameDeviceReturnsToTheSameNetwork()
    {
        var chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                     "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        var device = UserAgentParser.Parse(chrome);

        Trail(
            Entry(SecurityEventType.LoginSuccess, KnownAddress, device),
            Entry(SecurityEventType.LoginSuccess, KnownAddress, device));

        var suspicious = await detector.IsSuspiciousAsync(userId, KnownAddress, chrome);

        suspicious.Should().BeFalse("the same browser on the same connection is the owner");
    }
}
