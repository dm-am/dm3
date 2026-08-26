using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Authorization;

/// <summary>
/// The one place that says the empty user id may not be compared.
/// </summary>
/// <remarks>
/// The rule exists because two habits of this codebase meet on Guid.Empty: the
/// guest subject carries it as its own id, and so does every field a projection
/// left unset. Compared as if it were an identity, those two match — which is
/// how a post projection that filled the text and forgot the game master handed
/// a reader who is nobody the master's sight of every [private] block.
/// </remarks>
public class AnonymousIdentityShould
{
    private static readonly Guid Someone = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SomeoneElse = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ReadTheEmptyIdAsTheAbsenceOfOne()
    {
        AnonymousIdentity.Is(Guid.Empty).Should().BeTrue();
        AnonymousIdentity.Is(Someone).Should().BeFalse();
    }

    [Fact]
    public void GiveNoIdForASubjectThatIsNotSignedIn()
    {
        AnonymousIdentity.Of(null).Should().BeNull();
        AnonymousIdentity.Of(Subject(Guid.Empty, authenticated: false)).Should().BeNull();
    }

    [Fact]
    public void GiveNoIdForASignedInSubjectCarryingTheEmptyId()
    {
        // Nothing builds such a subject today, and the comparisons that read it
        // must not be the reason that stays true.
        AnonymousIdentity.Of(Subject(Guid.Empty, authenticated: true)).Should().BeNull();
    }

    [Fact]
    public void GiveTheIdOfASignedInSubject()
    {
        AnonymousIdentity.Of(Subject(Someone, authenticated: true)).Should().Be(Someone);
    }

    [Fact]
    public void KeepEveryRealIdOnceWhenFilteringASet()
    {
        AnonymousIdentity
            .WithoutAnonymous([Guid.Empty, Someone, SomeoneElse, Someone])
            .Should().Equal(Someone, SomeoneElse);
    }

    [Fact]
    public void AnswerWithAnEmptySetWhenThereIsNothingToKeep()
    {
        AnonymousIdentity.WithoutAnonymous(null).Should().BeEmpty();
        AnonymousIdentity.WithoutAnonymous([Guid.Empty]).Should().BeEmpty();
    }

    private static IAuthorizationSubject Subject(Guid userId, bool authenticated) =>
        new TestSubject
        {
            UserId = userId,
            Role = authenticated ? UserRole.RegularUser : UserRole.Guest,
            IsAuthenticated = authenticated,
            AccessPolicy = AccessPolicy.NotSpecified
        };

    private sealed class TestSubject : IAuthorizationSubject
    {
        public Guid UserId { get; init; }
        public UserRole Role { get; init; }
        public bool IsAuthenticated { get; init; }
        public AccessPolicy AccessPolicy { get; init; }
    }
}
