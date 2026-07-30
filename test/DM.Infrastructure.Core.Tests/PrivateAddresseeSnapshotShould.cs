using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Closes the loop the addressee rule needs and never had: text is saved, the
/// snapshot is built from it, and the same text is rendered back through the
/// visibility filter with that snapshot.
/// </summary>
/// <remarks>
/// The two halves used to be tested apart — the filter against a hand-written
/// dictionary, the syntax against the front end — and the field the filter reads
/// was written by nothing at all, so rule 2 of BBCODE_RENDERING.md (addressee-
/// forever) never fired for anybody. Every assertion here spans both halves: the
/// key the writer produces has to be the one the parser reports, or the test goes
/// red the way production went silent.
/// </remarks>
public class PrivateAddresseeSnapshotShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AnnaOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BorisOwner = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>Characters with access to the room the post goes into.</summary>
    private static readonly PrivateAddressee[] Roster =
    {
        new("Анна", AnnaOwner),
        new("Борис", BorisOwner)
    };

    /// <summary>
    /// Every spelling a client can produce, including the one-sided quote that
    /// used to defeat normalisation and hand the block to the whole room.
    /// </summary>
    [Theory]
    [InlineData("[private=Анна]секрет[/private]")]
    [InlineData("[private=\"Анна\"]секрет[/private]")]
    [InlineData("[PRIVATE=Анна]секрет[/PRIVATE]")]
    [InlineData("[Private=\"Анна]секрет[/Private]")]
    [InlineData("[private= Анна ]секрет[/private]")]
    public void ReachTheAddressee_AndNobodyElse_WhicheverWayTheAttributeIsWritten(string text)
    {
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        Render(text, snapshot, AnnaOwner).Should().Contain("секрет");
        Render(text, snapshot, BorisOwner).Should().NotContain("секрет");
        Render(text, snapshot, Stranger).Should().NotContain("секрет");
    }

    [Fact]
    public void TellTwoBlocksApart()
    {
        const string text = "[private=Анна]анне[/private] всем [private=Борис]борису[/private]";
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        var anna = Render(text, snapshot, AnnaOwner);
        anna.Should().Contain("анне");
        anna.Should().NotContain("борису");

        var boris = Render(text, snapshot, BorisOwner);
        boris.Should().Contain("борису");
        boris.Should().NotContain("анне");

        var author = Render(text, snapshot, AuthorId);
        author.Should().Contain("анне");
        author.Should().Contain("борису");

        var stranger = Render(text, snapshot, Stranger);
        stranger.Should().NotContain("анне");
        stranger.Should().NotContain("борису");
        stranger.Should().Contain("всем");
    }

    [Fact]
    public void ReachEveryOwnerNamedInOneBlock()
    {
        const string text = "[private=Анна, Борис]секрет[/private]";
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        Render(text, snapshot, AnnaOwner).Should().Contain("секрет");
        Render(text, snapshot, BorisOwner).Should().Contain("секрет");
        Render(text, snapshot, Stranger).Should().NotContain("секрет");
    }

    [Fact]
    public void ResolveNobodyForANameThatIsNotInTheRoom()
    {
        // A snapshot can only name characters that already read the room, so it
        // hands out no access room membership did not hand out first.
        PrivateAddresseeSnapshot
            .Build("[private=Виктор]секрет[/private]", Roster)
            .Should().Be(PrivateAddresseeSnapshot.Empty);
    }

    [Fact]
    public void ResolveNobodyForABlockThatNamesNoOne()
    {
        PrivateAddresseeSnapshot
            .Build("[private]секрет[/private]", Roster)
            .Should().Be(PrivateAddresseeSnapshot.Empty);
    }

    [Fact]
    public void KeepAnAddresseeWhoseCharacterHasLeftTheRoom()
    {
        // Addressee-forever: the block was resolved once, and an edit made after
        // the character left must not revoke the player it was written to.
        const string text = "[private=Анна]секрет[/private]";
        var atFirstSave = PrivateAddresseeSnapshot.Build(text, Roster);

        var afterSheLeft = PrivateAddresseeSnapshot.Build(
            text, atFirstSave, Array.Empty<PrivateAddressee>());

        afterSheLeft.Should().Be(atFirstSave);
        Render(text, afterSheLeft, AnnaOwner).Should().Contain("секрет");
    }

    [Fact]
    public void ResolveABlockTheEditIntroduced_AndDropOneItRemoved()
    {
        var before = PrivateAddresseeSnapshot.Build("[private=Анна]анне[/private]", Roster);
        const string after = "[private=Борис]борису[/private]";

        var rebuilt = PrivateAddresseeSnapshot.Build(after, before, Roster);

        Render(after, rebuilt, BorisOwner).Should().Contain("борису");
        PrivateAddresseeSnapshot.Parse(rebuilt).Should().NotContainKey("Анна");
    }

    [Fact]
    public void ReadBackNothingFromAMalformedSnapshot()
    {
        // Failure direction is closed: unreadable state denies through this rule
        // instead of granting through it.
        PrivateAddresseeSnapshot.Parse("not json").Should().BeEmpty();
        PrivateAddresseeSnapshot.Parse("[]").Should().BeEmpty();
        PrivateAddresseeSnapshot.Parse(null).Should().BeEmpty();
    }

    private string Render(string text, string snapshotJson, Guid viewerId)
    {
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = new TestSubject
            {
                UserId = viewerId,
                Role = UserRole.RegularUser,
                IsAuthenticated = true,
                AccessPolicy = AccessPolicy.NotSpecified
            },
            PostAuthorUserId = AuthorId,
            PrivateAddresseeOwnerUserIdsByAttribute = PrivateAddresseeSnapshot.Parse(snapshotJson)
        };

        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        return wrapper.RenderHtml(text, ctx);
    }

    private sealed class TestSubject : IAuthorizationSubject
    {
        public Guid UserId { get; init; }
        public UserRole Role { get; init; }
        public bool IsAuthenticated { get; init; }
        public AccessPolicy AccessPolicy { get; init; }
    }
}
