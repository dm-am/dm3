using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Authorization;

/// <summary>
/// The ban rules the owner defined, pinned one clause at a time. The ordinary
/// ban was written by the moderation service and read by nothing at all, so
/// every one of these assertions describes behavior that did not exist.
/// </summary>
public class AccessRestrictionsShould
{
    private sealed record Subject(AccessPolicy AccessPolicy) : IAuthorizationSubject
    {
        public Guid UserId => Guid.Empty;
        public UserRole Role => UserRole.RegularUser;
        public bool IsAuthenticated => true;
    }

    private static IAuthorizationSubject Unrestricted => new Subject(AccessPolicy.NotSpecified);
    private static IAuthorizationSubject DemocraticallyBanned => new Subject(AccessPolicy.DemocraticBan);
    private static IAuthorizationSubject FullyBanned => new Subject(AccessPolicy.FullBan);

    [Fact]
    public void LetAnUnrestrictedUserSpeakAnywhere()
    {
        Unrestricted.MaySpeak().Should().BeTrue();
        Unrestricted.MaySpeak(inOwnSpace: true).Should().BeTrue();
    }

    [Fact]
    public void SilencePublicSpeechUnderTheOrdinaryBan()
    {
        DemocraticallyBanned.MaySpeak().Should().BeFalse();
    }

    [Fact]
    public void LeaveOwnSpaceOpenUnderTheOrdinaryBan()
    {
        DemocraticallyBanned.MaySpeak(inOwnSpace: true).Should().BeTrue();
    }

    [Fact]
    public void SilenceEverythingUnderAFullBan()
    {
        FullyBanned.MaySpeak().Should().BeFalse();
        // Owning the space grants nothing here: a full ban forbids sending
        // anything at all
        FullyBanned.MaySpeak(inOwnSpace: true).Should().BeFalse();
    }

    [Fact]
    public void TakeTheStricterBanWhenBothApply()
    {
        var both = new Subject(AccessPolicy.DemocraticBan | AccessPolicy.FullBan);

        both.MaySpeak(inOwnSpace: true).Should().BeFalse();
    }
}
