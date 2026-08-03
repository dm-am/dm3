using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Testing;
using FluentAssertions;
using Moq;
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
    private readonly Mock<ISecurityAuditService> _auditService;
    private readonly SuspiciousLoginDetector _detector;

    public SuspiciousLoginDetectorShould()
    {
        _auditService = Mock<ISecurityAuditService>();
        _detector = new SuspiciousLoginDetector(_auditService.Object);
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

    [Fact]
    public async Task NotTakeAFailedAttemptAsProofTheAddressIsTheOwners()
    {
        Trail(
            Entry(SecurityEventType.LoginSuccess, NewAddress),
            Entry(SecurityEventType.LoginFailure, NewAddress),
            Entry(SecurityEventType.LoginSuccess, KnownAddress));

        var suspicious = await _detector.IsSuspiciousAsync(_userId, NewAddress, "agent");

        suspicious.Should().BeTrue();
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
        _auditService.Setup(s => s.GetLoginHistoryAsync(_userId, It.IsAny<int>()))
            .ReturnsAsync(newestFirst);

    private static SecurityAuditEntry Entry(SecurityEventType type, string address) =>
        new() { EventType = type, IpAddress = address };
}
