using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using DM.Testing.Dsl;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates private game content: who may edit, accept, kill or exile a character.
/// The resolver is thirteen guarded arms, and most of the guards are the point —
/// an arm that loses its guard silently widens access.
/// </summary>
public class CharacterIntentionResolverShould : UnitTestBase
{
    private readonly CharacterIntentionResolver resolver = new();

    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid MasterId = Guid.NewGuid();
    private static readonly Guid AssistantId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static CharacterToUpdate Character(
        CharacterStatus status = CharacterStatus.Active,
        ModuleStatus gameStatus = ModuleStatus.Active,
        bool isNpc = false,
        bool isDead = false,
        bool isPlayerLeft = false,
        CharacterAccessPolicy accessPolicy = CharacterAccessPolicy.NoAccess) => new()
    {
        AuthorId = isNpc ? Guid.Empty : PlayerId,
        GameMasterId = MasterId,
        GameAssistantIds = [AssistantId],
        GameStatus = gameStatus,
        Status = status,
        IsNpc = isNpc,
        IsDead = isDead,
        IsPlayerLeft = isPlayerLeft,
        AccessPolicy = accessPolicy,
    };

    // --- Ownership by the player ---

    [Theory]
    [InlineData(CharacterIntention.Edit)]
    [InlineData(CharacterIntention.EditPrivacySettings)]
    [InlineData(CharacterIntention.Delete)]
    public void AllowTheAuthorInAnActiveGame(CharacterIntention intention)
    {
        var player = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(player, intention, Character()).Should().BeTrue();
    }

    [Theory]
    [InlineData(CharacterIntention.Edit, ModuleStatus.Draft)]
    [InlineData(CharacterIntention.Edit, ModuleStatus.Closed)]
    [InlineData(CharacterIntention.EditPrivacySettings, ModuleStatus.Closed)]
    [InlineData(CharacterIntention.Delete, ModuleStatus.Closed)]
    public void ForbidTheAuthorWhenTheGameIsNotActive(CharacterIntention intention, ModuleStatus gameStatus)
    {
        var player = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(player, intention, Character(gameStatus: gameStatus)).Should().BeFalse();
    }

    [Theory]
    [InlineData(CharacterIntention.Edit)]
    [InlineData(CharacterIntention.Delete)]
    [InlineData(CharacterIntention.Accept)]
    [InlineData(CharacterIntention.Kill)]
    public void ForbidAStrangerEvenAsAdministrator(CharacterIntention intention)
    {
        // Site role does not open someone else's character: the resolver only
        // knows character ownership and game ownership.
        var admin = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(admin, intention, Character()).Should().BeFalse();
    }

    // --- Ownership by the game ---

    [Fact]
    public void ForbidTheMasterFromEditingAPlayerCharacterByDefault()
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Edit, Character()).Should().BeFalse();
    }

    [Fact]
    public void AllowTheMasterToEditAPlayerCharacterWhenThePlayerGrantedIt()
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var granted = Character(accessPolicy: CharacterAccessPolicy.EditAllowed);

        resolver.IsAllowed(master, CharacterIntention.Edit, granted).Should().BeTrue();
    }

    [Fact]
    public void NotTreatPostEditAllowedAsEditAllowed()
    {
        // The two flags are separate bits; granting post editing must not grant
        // sheet editing.
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var granted = Character(accessPolicy: CharacterAccessPolicy.PostEditAllowed);

        resolver.IsAllowed(master, CharacterIntention.Edit, granted).Should().BeFalse();
    }

    [Fact]
    public void AllowTheAssistantEverythingTheMasterMay()
    {
        var assistant = Create.User(AssistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(assistant, CharacterIntention.EditMasterSettings, Character())
            .Should().BeTrue();
    }

    [Fact]
    public void AllowTheMasterToEditAndDeleteAnNpcWithoutAnyGrant()
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();
        var npc = Character(isNpc: true);

        resolver.IsAllowed(master, CharacterIntention.Edit, npc).Should().BeTrue();
        resolver.IsAllowed(master, CharacterIntention.Delete, npc).Should().BeTrue();
    }

    [Fact]
    public void ForbidTheMasterFromDeletingAPlayerCharacter()
    {
        // Deleting is the player's own call; the master exiles instead.
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Delete, Character()).Should().BeFalse();
    }

    // --- Application review ---

    [Theory]
    [InlineData(CharacterStatus.UnderReview, true)]
    [InlineData(CharacterStatus.Declined, true)]
    [InlineData(CharacterStatus.Active, false)]
    [InlineData(CharacterStatus.Retired, false)]
    public void AllowAcceptOnlyFromReviewOrDeclined(CharacterStatus status, bool expected)
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Accept, Character(status))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(CharacterStatus.UnderReview, true)]
    [InlineData(CharacterStatus.Declined, false)]
    [InlineData(CharacterStatus.Active, false)]
    public void AllowDeclineOnlyFromReview(CharacterStatus status, bool expected)
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Decline, Character(status))
            .Should().Be(expected);
    }

    // --- Life cycle ---

    [Theory]
    [InlineData(CharacterStatus.Active, true)]
    [InlineData(CharacterStatus.Retired, false)]
    [InlineData(CharacterStatus.UnderReview, false)]
    public void AllowKillAndExileOnlyOnAnActiveCharacter(CharacterStatus status, bool expected)
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Kill, Character(status)).Should().Be(expected);
        resolver.IsAllowed(master, CharacterIntention.Exile, Character(status)).Should().Be(expected);
    }

    [Fact]
    public void AllowResurrectOnlyOnARetiredCharacterThatIsActuallyDead()
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Resurrect,
            Character(CharacterStatus.Retired, isDead: true)).Should().BeTrue();

        // Retired because the player left, not because the character died.
        resolver.IsAllowed(master, CharacterIntention.Resurrect,
            Character(CharacterStatus.Retired, isDead: false)).Should().BeFalse();
    }

    [Fact]
    public void AllowReturnOnlyToAPlayerWhoLeftVoluntarily()
    {
        var player = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(player, CharacterIntention.Return,
            Character(CharacterStatus.Retired, isPlayerLeft: true)).Should().BeTrue();

        // Exiled, not left: returning is not the player's to decide.
        resolver.IsAllowed(player, CharacterIntention.Return,
            Character(CharacterStatus.Retired, isPlayerLeft: false)).Should().BeFalse();
    }

    [Fact]
    public void AllowLeaveOnlyWhileTheCharacterIsActive()
    {
        var player = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(player, CharacterIntention.Leave, Character())
            .Should().BeTrue();
        resolver.IsAllowed(player, CharacterIntention.Leave, Character(CharacterStatus.Retired))
            .Should().BeFalse();
    }

    [Fact]
    public void RefuseAnIntentionItDoesNotKnow()
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, (CharacterIntention)int.MaxValue, Character())
            .Should().BeFalse();
    }
}
