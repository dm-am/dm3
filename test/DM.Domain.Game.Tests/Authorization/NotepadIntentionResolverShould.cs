using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates the three notepads of a game. Two of them hold what somebody is
/// keeping from the rest of the table: the notepad of the game and the notes
/// its leads keep about a character hold plot the players have not reached
/// yet, so a wrong allow there does not merely leak data, it spoils the game.
/// The third runs the other way — a player's own notes are closed to the
/// master, and a wrong allow there breaks the promise the notepad is made of.
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
    [InlineData(GameRole.Mentor)]
    public void KeepAPlayerNotepadShutToTheGameLeads(GameRole role)
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, role);

        // The one notepad of a game its master cannot open. Running the game is
        // not a key to what a player writes for themselves about their own
        // character; the notes the leads keep about that character are a
        // notepad of their own, further down.
        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeFalse();
    }

    [Fact]
    public void KeepAPlayerNotepadShutToAVisitorWithNoRoleInTheGame()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Guest).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId);

        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeFalse();
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

    #region Character master notepad

    [Theory]
    [InlineData(GameRole.Master)]
    [InlineData(GameRole.Assistant)]
    public void OpenTheNotesKeptAboutACharacterToTheGameLeads(GameRole role)
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        // The owner of the character is named in the context and is beside the
        // point here: this notepad is answered by the game role alone, which is
        // what makes it exist for every character and not only for an NPC.
        var notepad = Notepad(NotepadType.CharacterMaster, PlayerId, role);

        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeTrue();
    }

    [Fact]
    public void KeepTheNotesKeptAboutACharacterShutToThePlayerWhoOwnsIt()
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.CharacterMaster, PlayerId, GameRole.Player);

        // Owning the character is what this notepad is about, not a right to
        // read it - the mirror image of the player notepad above.
        resolver.IsAllowed(user, NotepadIntention.Read, notepad).Should().BeFalse();
    }

    [Theory]
    [InlineData(GameRole.Player)]
    [InlineData(GameRole.Reader)]
    [InlineData(GameRole.Applicant)]
    [InlineData(GameRole.Mentor)]
    [InlineData(GameRole.None)]
    public void KeepTheNotesKeptAboutACharacterShutToEveryoneElseInTheGame(GameRole role)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(NotepadType.CharacterMaster, PlayerId, role))
            .Should().BeFalse();
    }

    [Fact]
    public void KeepTheNotesKeptAboutACharacterShutToAVisitorWithNoRoleInTheGame()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Guest).Please();

        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(NotepadType.CharacterMaster, PlayerId))
            .Should().BeFalse();
    }

    [Fact]
    public void KeepTheNotesKeptAboutACharacterShutToSiteAdministration()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, NotepadIntention.Read, Notepad(NotepadType.CharacterMaster, PlayerId))
            .Should().BeFalse();
    }

    [Fact]
    public void LetTheGameMasterDeleteAnAssistantsNoteAboutACharacter()
    {
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var entry = Entry(StrangerId, NotepadType.CharacterMaster, PlayerId, GameRole.Master);

        // The same split the notepad of the game has: the master answers for
        // what the notepads hold and may remove any entry, but rewriting an
        // entry that would still stand under its author's name is nobody's.
        resolver.IsAllowed(user, NotepadIntention.Delete, entry).Should().BeTrue();
        resolver.IsAllowed(user, NotepadIntention.Edit, entry).Should().BeFalse();
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
        var entry = Entry(StrangerId, NotepadType.Master, null, GameRole.Master);

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
