using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing;
using DM.Testing.Dsl;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates the game notepads. The master notepad holds what the game master is
/// keeping from the table — plot the players have not reached yet — so a wrong
/// allow here does not merely leak data, it spoils the game.
/// </summary>
public class NotepadIntentionResolverShould : UnitTestBase
{
    private readonly NotepadIntentionResolver resolver = new();

    private static readonly Guid MasterId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static NotepadAuthContext Notepad(
        NotepadType type,
        Guid? characterOwnerId,
        params GameRole[] roles) =>
        new()
        {
            NotepadType = type,
            ContainerId = Guid.NewGuid(),
            CharacterOwnerId = characterOwnerId,
            GameRoles = roles
        };

    /// <summary>The same notepad, asked about one entry standing in it.</summary>
    private static NotepadAuthContext Entry(
        Guid authorId,
        NotepadType type,
        Guid? characterOwnerId,
        params GameRole[] roles)
    {
        var context = Notepad(type, characterOwnerId, roles);
        context.AuthorId = authorId;
        return context;
    }

    #region Master notepad

    [Theory]
    [InlineData(GameRole.Master)]
    [InlineData(GameRole.Assistant)]
    public void OpenTheMasterNotepadToTheGameLeads(GameRole role)
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(NotepadType.Master, null, role))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(GameRole.Player)]
    [InlineData(GameRole.Reader)]
    [InlineData(GameRole.Applicant)]
    [InlineData(GameRole.Mentor)]
    [InlineData(GameRole.None)]
    public void KeepTheMasterNotepadShutToEveryoneElseInTheGame(GameRole role)
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // The curating mentor is in this list too: supervising a game lets a
        // mentor speak in it, it does not open the master's private notes.
        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(NotepadType.Master, null, role))
            .Should().BeFalse();
    }

    [Fact]
    public void KeepTheMasterNotepadShutToSiteAdministration()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        // Only game roles are consulted. No site rank reaches into a game's notes.
        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(NotepadType.Master, null))
            .Should().BeFalse();
    }

    #endregion

    #region Player notepad

    [Fact]
    public void LetACharacterOwnerIntoTheirOwnPlayerNotepad()
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, GameRole.Player);

        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeTrue();
    }

    [Fact]
    public void NotLetOnePlayerIntoAnothersNotepad()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, GameRole.Player);

        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeFalse();
    }

    [Theory]
    [InlineData(GameRole.Master)]
    [InlineData(GameRole.Assistant)]
    public void LetTheGameLeadsIntoAPlayerNotepad(GameRole role)
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, role);

        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeTrue();
    }

    [Fact]
    public void NotOpenAPlayerNotepadThatHasNoOwner()
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, null, GameRole.Player);

        // An unresolved owner must fail closed rather than match whoever asks.
        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeFalse();
    }

    #endregion

    #region Notepad types this resolver does not serve

    [Theory]
    [InlineData(NotepadType.Blog)]
    [InlineData(NotepadType.User)]
    public void RefuseNotepadTypesItDoesNotOwnEvenForTheGameLeads(NotepadType type)
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        // Blog and personal notepads are somebody else's authorization question.
        // Falling through to a deny keeps this resolver from half-answering it.
        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(type, null, GameRole.Master))
            .Should().BeFalse();
    }

    #endregion

    #region Editing and deleting a single entry

    [Theory]
    [InlineData(NotepadIntention.Read)]
    [InlineData(NotepadIntention.Create)]
    public void DecideReadingAndCreatingOnAccessAlone(NotepadIntention intention)
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, GameRole.Player);

        // Neither question is about an entry that already exists, so neither
        // consults an author. A shared notepad whose entries only their authors
        // could read would not be shared.
        resolver.IsAllowed(user, intention, notepad).Should().BeTrue();
    }

    [Fact]
    public void LetTheAuthorEditAndDeleteTheirOwnEntry()
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var entry = Entry(PlayerId, NotepadType.Player, PlayerId, GameRole.Player);

        resolver.IsAllowed(user, NotepadIntention.Edit, entry).Should().BeTrue();
        resolver.IsAllowed(user, NotepadIntention.Delete, entry).Should().BeTrue();
    }

    [Fact]
    public void NotLetTheGameMasterEditAnEntryTheyDidNotWrite()
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var entry = Entry(PlayerId, NotepadType.Player, PlayerId, GameRole.Master);

        // Reading it and removing it stay open to the master. Rewriting another
        // person's note, which then still stands under their name, does not.
        resolver.IsAllowed(user, NotepadIntention.Read, entry).Should().BeTrue();
        resolver.IsAllowed(user, NotepadIntention.Delete, entry).Should().BeTrue();
        resolver.IsAllowed(user, NotepadIntention.Edit, entry).Should().BeFalse();
    }

    [Fact]
    public void LetAnAssistantTouchOnlyTheEntriesTheyWroteThemselves()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var own = Entry(StrangerId, NotepadType.Master, null, GameRole.Assistant);
        var mastersOwn = Entry(MasterId, NotepadType.Master, null, GameRole.Assistant);

        // The whole master notepad is open to an assistant for reading, which is
        // exactly why deleting from it is the master's right and not every lead's.
        resolver.IsAllowed(user, NotepadIntention.Edit, own).Should().BeTrue();
        resolver.IsAllowed(user, NotepadIntention.Delete, own).Should().BeTrue();
        resolver.IsAllowed(user, NotepadIntention.Edit, mastersOwn).Should().BeFalse();
        resolver.IsAllowed(user, NotepadIntention.Delete, mastersOwn).Should().BeFalse();
    }

    [Fact]
    public void LetTheGameMasterDeleteAnEntryWrittenBySomebodyElse()
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var entry = Entry(StrangerId, NotepadType.Master, null, GameRole.Master);

        resolver.IsAllowed(user, NotepadIntention.Delete, entry).Should().BeTrue();
    }

    [Theory]
    [InlineData(NotepadIntention.Edit)]
    [InlineData(NotepadIntention.Delete)]
    public void RefuseToTouchAnEntryWhoseAuthorWasNotSupplied(NotepadIntention intention)
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, GameRole.Player);

        // Both intentions are decided by the author, so a context carrying none
        // fails closed rather than reading "nobody" as the one who is asking.
        resolver.IsAllowed(user, intention, notepad).Should().BeFalse();
    }

    [Theory]
    [InlineData(NotepadIntention.Read)]
    [InlineData(NotepadIntention.Create)]
    [InlineData(NotepadIntention.Edit)]
    [InlineData(NotepadIntention.Delete)]
    public void RefuseEveryIntentionToSomebodyWithoutAccess(NotepadIntention intention)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var entry = Entry(StrangerId, NotepadType.Player, PlayerId, GameRole.Player);

        // Authorship does not open a notepad the asker cannot reach: access is
        // answered first, and somebody who once wrote in another player's
        // notepad keeps no key to it.
        resolver.IsAllowed(user, intention, entry).Should().BeFalse();
    }

    #endregion
}
