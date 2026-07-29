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

    #region Behaviour as found

    [Theory]
    [InlineData(NotepadIntention.Read)]
    [InlineData(NotepadIntention.Create)]
    [InlineData(NotepadIntention.Edit)]
    [InlineData(NotepadIntention.Delete)]
    public void GiveTheSameAnswerToEveryNotepadIntention(NotepadIntention intention)
    {
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, GameRole.Player);

        // Behaviour as found, not a rule that was chosen. The resolver never
        // looks at the intention, so a player who may read their notepad may also
        // delete from it, and an assistant who may read the master notepad may
        // wipe it. NotepadAuthContext.AuthorId exists for exactly the per-entry
        // check that would separate these, and the only caller always writes it
        // as null. Pinned so that wiring AuthorId up is a deliberate change.
        resolver.IsAllowed(user, intention, notepad).Should().BeTrue();
    }

    [Theory]
    [InlineData(NotepadIntention.Read)]
    [InlineData(NotepadIntention.Delete)]
    public void GiveTheSameDenialToEveryNotepadIntention(NotepadIntention intention)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var notepad = Notepad(NotepadType.Player, PlayerId, GameRole.Player);

        resolver.IsAllowed(user, intention, notepad).Should().BeFalse();
    }

    #endregion
}
