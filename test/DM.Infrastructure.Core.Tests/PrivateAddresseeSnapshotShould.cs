using System;
using System.Text.Json;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Text is saved, the snapshot is built from it, and the same text is rendered back with that snapshot:
/// the key the writer produces is the one the parser reports.
/// </summary>
public class PrivateAddresseeSnapshotShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid AnnaOwner = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BorisOwner = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Guid AnnaCharacter = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BorisCharacter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    /// <summary>Characters with access to the room the post goes into.</summary>
    private static readonly PrivateAddressee[] Roster =
    {
        new(AnnaCharacter, "Анна", AnnaOwner),
        new(BorisCharacter, "Борис", BorisOwner)
    };

    /// <summary>
    /// The room as it stood when the post was written: Anna's character is
    /// called "Чак".
    /// </summary>
    private static readonly PrivateAddressee[] RosterBeforeTheRenames =
    {
        new(AnnaCharacter, "Чак", AnnaOwner)
    };

    /// <summary>
    /// The same room after two renames: Anna's character is now "Владимир", and
    /// the name it gave up has been taken by a character of Boris's.
    /// </summary>
    private static readonly PrivateAddressee[] RosterAfterTheRenames =
    {
        new(AnnaCharacter, "Владимир", AnnaOwner),
        new(BorisCharacter, "Чак", BorisOwner)
    };

    /// <summary>The same room, after only the first of those renames.</summary>
    private static readonly PrivateAddressee[] RosterAfterTheFirstRename =
    {
        new(AnnaCharacter, "Владимир", AnnaOwner)
    };

    private const string AddressedToChuck = "[private=\"Чак\"]секрет[/private]";

    [Fact]
    public void ReachTheAddresseeAndNobodyElse()
    {
        const string text = "[private=\"Анна\"]секрет[/private]";
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        Render(text, snapshot, AnnaOwner).Should().Contain("секрет");
        Render(text, snapshot, BorisOwner).Should().NotContain("секрет");
        Render(text, snapshot, Stranger).Should().NotContain("секрет");
    }

    /// <summary>
    /// A spelling the grammar does not accept addresses nobody, whichever name it
    /// seems to carry: the block stays with the author and the leads, and the save
    /// path refuses the text and says how to write it.
    /// </summary>
    /// <remarks>Reading a name out of an unquoted value means guessing where it ends.</remarks>
    [Theory]
    [InlineData("[private=Анна]секрет[/private]")]
    [InlineData("[PRIVATE=Анна]секрет[/private]")]
    [InlineData("[Private=\"Анна]секрет[/private]")]
    [InlineData("[private= Анна ]секрет[/private]")]
    [InlineData("[private to=\"Анна\"]секрет[/private]")]
    public void ReachNoAddresseeWhenTheAttributeIsMisspelled(string text)
    {
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        snapshot.Should().Be(PrivateAddresseeSnapshot.Empty);
        Render(text, snapshot, AnnaOwner).Should().NotContain("секрет");
        Render(text, snapshot, Stranger).Should().NotContain("секрет");
        Render(text, snapshot, BbVisibilityRender.AuthorId).Should().Contain("секрет");
        PrivateBlockMarkup.IsBalanced(text).Should().BeFalse();
    }

    [Fact]
    public void TellTwoBlocksApart()
    {
        const string text = "[private=\"Анна\"]анне[/private] всем [private=\"Борис\"]борису[/private]";
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        var anna = Render(text, snapshot, AnnaOwner);
        anna.Should().Contain("анне");
        anna.Should().NotContain("борису");

        var boris = Render(text, snapshot, BorisOwner);
        boris.Should().Contain("борису");
        boris.Should().NotContain("анне");

        var author = Render(text, snapshot, BbVisibilityRender.AuthorId);
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
        const string text = "[private=\"Анна, Борис\"]секрет[/private]";
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
            .Build("[private=\"Виктор\"]секрет[/private]", Roster)
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
        const string text = "[private=\"Анна\"]секрет[/private]";
        var atFirstSave = PrivateAddresseeSnapshot.Build(text, Roster);

        var afterSheLeft = PrivateAddresseeSnapshot.Build(
            text, atFirstSave, Array.Empty<PrivateAddressee>());

        afterSheLeft.Should().Be(atFirstSave);
        Render(text, afterSheLeft, AnnaOwner).Should().Contain("секрет");
    }

    [Fact]
    public void ResolveABlockTheEditIntroducedAndDropOneItRemoved()
    {
        var before = PrivateAddresseeSnapshot.Build("[private=\"Анна\"]анне[/private]", Roster);
        const string after = "[private=\"Борис\"]борису[/private]";

        var rebuilt = PrivateAddresseeSnapshot.Build(after, before, Roster);

        Render(after, rebuilt, BorisOwner).Should().Contain("борису");
        PrivateAddresseeSnapshot.Read(rebuilt).OwnerUserIdsByAttribute.Should().NotContainKey("Анна");
    }

    /// <summary>
    /// A name is not an identity: the same key may mean one character in a frozen block and another later,
    /// so the doubt is put to the author. Nothing leaks, delivery breaks.
    /// </summary>
    [Fact]
    public void RefuseASaveWhenTheNameNowBelongsToAnotherCharacter()
    {
        var frozen = PrivateAddresseeSnapshot.Build(AddressedToChuck, RosterBeforeTheRenames);

        var act = () => PrivateAddresseeSnapshot.Build(
            AddressedToChuck, frozen, RosterAfterTheRenames);

        act.Should().Throw<HttpException>()
            .WithMessage(PrivateAddresseeSnapshot.DescribeRenameRefusal("Чак"));
    }

    /// <summary>
    /// Not a refusal: the name still points at the character it was frozen to,
    /// so nothing is in doubt and the frozen answer stands.
    /// </summary>
    [Fact]
    public void KeepTheFrozenAddresseeWhenTheNameStillMeansTheSameCharacter()
    {
        var frozen = PrivateAddresseeSnapshot.Build(AddressedToChuck, RosterBeforeTheRenames);

        var rebuilt = PrivateAddresseeSnapshot.Build(
            AddressedToChuck, frozen, RosterBeforeTheRenames);

        rebuilt.Should().Be(frozen);
        Render(AddressedToChuck, rebuilt, AnnaOwner).Should().Contain("секрет");
    }

    /// <summary>
    /// Not a refusal: the name means nobody today, which is the ordinary shape
    /// of addressee-forever. The reader it was frozen to keeps the block.
    /// </summary>
    [Fact]
    public void KeepTheFrozenAddresseeWhenTheNameMeansNobodyToday()
    {
        var frozen = PrivateAddresseeSnapshot.Build(AddressedToChuck, RosterBeforeTheRenames);

        var rebuilt = PrivateAddresseeSnapshot.Build(
            AddressedToChuck, frozen, RosterAfterTheFirstRename);

        Render(AddressedToChuck, rebuilt, AnnaOwner).Should().Contain("секрет");
        Render(AddressedToChuck, rebuilt, BorisOwner).Should().NotContain("секрет");
    }

    /// <summary>
    /// Not a refusal: the author rewrote the tag with the addressee's current
    /// name. That is a key the post has never seen, it resolves against today's
    /// roster, and it lands on the same reader.
    /// </summary>
    [Fact]
    public void ResolveTheCurrentNameOfTheSameCharacterAsANewKey()
    {
        var frozen = PrivateAddresseeSnapshot.Build(AddressedToChuck, RosterBeforeTheRenames);
        const string renamed = "[private=\"Владимир\"]секрет[/private]";

        var rebuilt = PrivateAddresseeSnapshot.Build(renamed, frozen, RosterAfterTheRenames);

        PrivateAddresseeSnapshot.Read(rebuilt).OwnerUserIdsByAttribute.Should().ContainKey("Владимир")
            .WhoseValue.Should().BeEquivalentTo(new[] { AnnaOwner });
        Render(renamed, rebuilt, AnnaOwner).Should().Contain("секрет");
        Render(renamed, rebuilt, BorisOwner).Should().NotContain("секрет");
    }

    /// <summary>
    /// The recipients are the reader's only statement of who else is in on the
    /// block. Built from the tag text they name whoever the author typed,
    /// including names that reached nobody.
    /// </summary>
    [Fact]
    public void NameOnlyTheResolvedAddresseesInTheRecipients()
    {
        const string text = "[private=\"Анна, Виктор\"]секрет[/private]";
        var snapshot = PrivateAddresseeSnapshot.Build(text, Roster);

        var html = Render(text, snapshot, AnnaOwner);

        html.Should().Contain("data-recipients=\"DungeonMaster, Assistant, Анна\"");
        html.Should().NotContain("Виктор");
    }

    /// <summary>
    /// After a rename the recipients name the addressee as they are called now,
    /// while the tag keeps the name the author typed: the text is theirs, the
    /// recipients are the reader's.
    /// </summary>
    [Fact]
    public void NameTheAddresseeAsTheyAreCalledNowInTheRecipients()
    {
        var frozen = PrivateAddresseeSnapshot.Build(AddressedToChuck, RosterBeforeTheRenames);

        var rebuilt = PrivateAddresseeSnapshot.Build(
            AddressedToChuck, frozen, RosterAfterTheFirstRename);

        Render(AddressedToChuck, rebuilt, AnnaOwner)
            .Should().Contain("data-recipients=\"DungeonMaster, Assistant, Владимир\"");
    }

    /// <summary>
    /// The stored shape is a contract with rows nobody rewrites.
    /// </summary>
    [Fact]
    public void RecordTheCharacterBesideItsOwner()
    {
        var snapshot = PrivateAddresseeSnapshot.Build(AddressedToChuck, RosterBeforeTheRenames);

        using var document = JsonDocument.Parse(snapshot);
        var entry = document.RootElement.GetProperty("Чак")[0];
        entry.GetProperty("character").GetGuid().Should().Be(AnnaCharacter);
        entry.GetProperty("name").GetString().Should().Be("Чак");
        entry.GetProperty("owner").GetGuid().Should().Be(AnnaOwner);
    }

    [Fact]
    public void ReadBackNothingFromAMalformedSnapshot()
    {
        // Failure direction is closed: unreadable state denies through this rule
        // instead of granting through it.
        PrivateAddresseeSnapshot.Read("not json").OwnerUserIdsByAttribute.Should().BeEmpty();
        PrivateAddresseeSnapshot.Read("[]").OwnerUserIdsByAttribute.Should().BeEmpty();
        PrivateAddresseeSnapshot.Read(null).OwnerUserIdsByAttribute.Should().BeEmpty();
    }

    private string Render(string text, string snapshotJson, Guid viewerId)
    {
        var snapshot = PrivateAddresseeSnapshot.Read(snapshotJson);
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = BbVisibilityRender.Viewer(UserRole.RegularUser, viewerId),
            PostAuthorUserId = BbVisibilityRender.AuthorId,
            PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
            PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute
        };

        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);
        return wrapper.RenderHtml(text, ctx);
    }

}
