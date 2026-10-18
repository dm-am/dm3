using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
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
        CharacterAccessPolicy accessPolicy = CharacterAccessPolicy.NoAccess,
        bool isPlayerExiled = false) => new()
        {
            AuthorId = isNpc ? Guid.Empty : PlayerId,
            GameMasterId = MasterId,
            GameAssistantIds = [AssistantId],
            GameStatus = gameStatus,
            Status = status,
            IsNpc = isNpc,
            IsDead = isDead,
            IsPlayerLeft = isPlayerLeft,
            IsPlayerExiled = isPlayerExiled,
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

    [Theory]
    [InlineData(CharacterIntention.Edit)]
    [InlineData(CharacterIntention.EditPrivacySettings)]
    [InlineData(CharacterIntention.Delete)]
    [InlineData(CharacterIntention.Leave)]
    public void ForbidAGuestOnAnNpc(CharacterIntention intention)
    {
        // An NPC has no author, and the projection turns that null into
        // Guid.Empty — the very id an anonymous visitor carries. Ownership
        // must not match on it.
        var guest = Create.User(Guid.Empty).WithRole(UserRole.Guest).Please();

        resolver.IsAllowed(guest, intention, Character(isNpc: true)).Should().BeFalse();
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

        // Exiled, not dead: the way back from an exile is its own.
        resolver.IsAllowed(master, CharacterIntention.Resurrect,
            Character(CharacterStatus.Retired, isPlayerExiled: true)).Should().BeFalse();
    }

    /// <summary>
    /// Exile is the lead's move, so the lead may take it back: the master and his
    /// assistants, the same hands that exile. Nobody else, the player included.
    /// </summary>
    [Fact]
    public void LetTheLeadBringBackTheCharacterHeExiled()
    {
        var exiled = Character(CharacterStatus.Retired, isPlayerExiled: true);

        resolver.IsAllowed(Create.User(MasterId).WithRole(UserRole.RegularUser).Please(),
            CharacterIntention.Readmit, exiled).Should().BeTrue();
        resolver.IsAllowed(Create.User(AssistantId).WithRole(UserRole.RegularUser).Please(),
            CharacterIntention.Readmit, exiled).Should().BeTrue();

        resolver.IsAllowed(Create.User(PlayerId).WithRole(UserRole.RegularUser).Please(),
            CharacterIntention.Readmit, exiled).Should().BeFalse("the player was thrown out, he does not let himself back");
        resolver.IsAllowed(Create.User(StrangerId).WithRole(UserRole.Admin).Please(),
            CharacterIntention.Readmit, exiled).Should().BeFalse("the moderation does not run the roster");
    }

    /// <summary>
    /// Readmit undoes an exile and nothing else: not a death, and not a character
    /// that is still in play.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatus.Retired, true, false)]
    [InlineData(CharacterStatus.Retired, false, false)]
    [InlineData(CharacterStatus.Active, false, true)]
    public void BringBackOnlyACharacterThatWasExiled(CharacterStatus status, bool isDead, bool isPlayerExiled)
    {
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, CharacterIntention.Readmit,
            Character(status, isDead: isDead, isPlayerExiled: isPlayerExiled)).Should().BeFalse();
    }

    /// <summary>
    /// A character its player took out of the game comes back by nobody's move:
    /// not the player's, not the lead's, not the moderation's. Every intention
    /// that puts a character back into play is asked; the transitions that would
    /// reach them from Retired are held by CharacterStatusTransitionsShould.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BringBackNoCharacterItsPlayerTookOut(bool alsoDead, bool alsoExiled)
    {
        // Left, and marked dead or exiled by the lead as well: leaving still wins.
        var left = Character(CharacterStatus.Retired, isDead: alsoDead, isPlayerLeft: true,
            isPlayerExiled: alsoExiled);
        var everybody = new[]
        {
            Create.User(PlayerId).WithRole(UserRole.RegularUser).Please(),
            Create.User(MasterId).WithRole(UserRole.RegularUser).Please(),
            Create.User(AssistantId).WithRole(UserRole.RegularUser).Please(),
            Create.User(StrangerId).WithRole(UserRole.Moderator).Please(),
            Create.User(StrangerId).WithRole(UserRole.SeniorModerator).Please(),
            Create.User(StrangerId).WithRole(UserRole.Admin).Please(),
        };

        foreach (var user in everybody)
        {
            resolver.IsAllowed(user, CharacterIntention.Accept, left).Should().BeFalse(user.Role.ToString());
            resolver.IsAllowed(user, CharacterIntention.Resurrect, left).Should().BeFalse(user.Role.ToString());
            resolver.IsAllowed(user, CharacterIntention.Readmit, left).Should().BeFalse(user.Role.ToString());
        }
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
