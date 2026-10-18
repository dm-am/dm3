using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Authorization;

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

    /// <summary>
    /// The refusal names what this ban forbids, and the two scopes forbid
    /// different things.
    /// </summary>
    /// <remarks>
    /// One sentence for both told a reader under the ordinary ban that
    /// commenting was closed to him, while his own game and his own blog stayed
    /// open and showed him the field.
    /// </remarks>
    [Fact]
    public void RefuseTheOrdinaryBanWithoutClosingOwnSpace()
    {
        var refusal = Record.Exception(() => DemocraticallyBanned.ThrowIfMayNotComment());

        refusal.Should().BeOfType<HttpException>();
        refusal!.Message.Should().Be("Во время бана нельзя комментировать форум и чужие игры и блоги");
    }

    [Fact]
    public void RefuseTheFullBanEverywhere()
    {
        var refusal = Record.Exception(() => FullyBanned.ThrowIfMayNotComment(inOwnSpace: true));

        refusal.Should().BeOfType<HttpException>();
        refusal!.Message.Should().Be("Во время полного бана писать нельзя нигде");
    }

    [Fact]
    public void SayNothingWhenTheCommentIsAllowed()
    {
        Record.Exception(() => DemocraticallyBanned.ThrowIfMayNotComment(inOwnSpace: true))
            .Should().BeNull();
        Record.Exception(() => Unrestricted.ThrowIfMayNotComment()).Should().BeNull();
    }
}
