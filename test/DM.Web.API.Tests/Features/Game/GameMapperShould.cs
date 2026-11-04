using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Shared.BbRendering;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainGame = DM.Domain.Game.Features.Games.Game;
using DomainGameDetails = DM.Domain.Game.Features.Games.GameDetails;
using DomainRecruitment = DM.Domain.Game.Features.Games.GameRecruitment;
using DomainGameAttributeSchema = DM.Domain.Game.Features.GameAttributeSchemas.GameAttributeSchema;
using DomainCharacterShortInfo = DM.Domain.Game.Features.Characters.CharacterShortInfo;
using DomainGameTag = DM.Domain.Game.Features.Games.GameTag;

namespace DM.Web.API.Tests.Features.Game;

public class GameMapperShould : UnitTestBase
{
    private static GameMapper CreateMapper() =>
        new(new AttributeSchemaMapper(), Mock<IIdentityProvider>());

    /// <summary>
    /// PATCH carries "not sent" as null and the write model keeps saying it: null leaves the tags alone, an
    /// empty list clears them.
    /// </summary>
    [Fact]
    public void KeepAnUnsentTagListNull()
    {
        var update = CreateMapper().ToUpdateGame(new UpdateGameRequest
        {
            Title = "Только имя"
        });

        update.Title.Should().Be("Только имя");
        update.Tags.Should().BeNull("null means \"leave the tags alone\", not \"clear them\"");
        update.HideDiceResult.Should().BeNull();
        update.ShowPrivateMessages.Should().BeNull();
        update.HidePostStats.Should().BeNull();
        update.CommentsAccessMode.Should().BeNull();
        update.IsRecruitmentOpen.Should().BeNull();
        update.RecruitmentPcLimit.Should().BeNull();
    }

    [Fact]
    public void CarryASentTagListThrough()
    {
        var update = CreateMapper().ToUpdateGame(new UpdateGameRequest
        {
            Tags = [3, 7]
        });

        update.Tags.Should().Equal(3, 7);
    }

    [Fact]
    public void CarryAnEmptyTagListAsAClear()
    {
        var update = CreateMapper().ToUpdateGame(new UpdateGameRequest
        {
            Tags = []
        });

        update.Tags.Should().NotBeNull().And.BeEmpty(
            "an empty list is an instruction to clear, distinct from an absent field");
    }

    /// <summary>
    /// A privacy block whose individual flag was not sent folds to null, not
    /// to a value - the lifted negation must not invent a setting.
    /// </summary>
    [Fact]
    public void FoldAPartialPrivacyBlockWithoutInventingValues()
    {
        var update = CreateMapper().ToUpdateGame(new UpdateGameRequest
        {
            PrivacySettings = new UpdateGamePrivacySettings
            {
                ViewDice = false
            }
        });

        update.HideDiceResult.Should().BeTrue();
        update.ShowPrivateMessages.Should().BeNull();
        update.HidePostStats.Should().BeNull();
        update.CommentsAccessMode.Should().BeNull();
    }

    /// <summary>
    /// An NPC roster line carries no owner, and the details response says so
    /// instead of refusing to be built.
    /// </summary>
    /// <remarks>The character row holds no author for an NPC, and Mapperly throws on a null into a non-nullable member.</remarks>
    [Fact]
    public void CarryAnNpcRosterLineWithoutAnOwner()
    {
        var details = CreateMapper().ToGameDetails(new DomainGameDetails
        {
            Master = Master(),
            Recruitment = new DomainRecruitment(),
            AttributeSchema = new DomainGameAttributeSchema(),
            Characters =
            [
                new DomainCharacterShortInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "Трактирщик",
                    Status = CharacterStatus.Active,
                    IsNpc = true,
                    Author = null
                }
            ]
        });

        var npc = details.Characters.Should().ContainSingle().Subject;
        npc.Name.Should().Be("Трактирщик");
        npc.Author.Should().BeNull("an NPC is run by the master and owned by nobody");
    }

    /// <summary>
    /// A character somebody plays still answers with the player who does - the
    /// null tolerance above must not have been bought by dropping every owner.
    /// </summary>
    [Fact]
    public void CarryTheOwnerOfAPlayedCharacter()
    {
        var authorId = Guid.NewGuid();
        var details = CreateMapper().ToGameDetails(new DomainGameDetails
        {
            Master = Master(),
            Recruitment = new DomainRecruitment(),
            AttributeSchema = new DomainGameAttributeSchema(),
            Characters =
            [
                new DomainCharacterShortInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "Следопыт",
                    Status = CharacterStatus.Active,
                    Author = new GeneralUser
                    {
                        UserId = authorId,
                        Username = "player",
                        Role = UserRole.RegularUser
                    }
                }
            ]
        });

        var played = details.Characters.Should().ContainSingle().Subject;
        played.Author.Should().NotBeNull();
        played.Author!.Id.Should().Be(authorId);
        played.Author.Username.Should().Be("player");
    }

    /// <summary>
    /// A guest holds no role: a game without a mentor must not read the guest's missing id as the mentor.
    /// </summary>
    [Fact]
    public void GiveAGuestNoParticipationInAGameWithoutAMentor()
    {
        var participation = MapperFor(Identities.Guest()).ParticipationOf(new DomainGame
        {
            Master = Master(),
            Recruitment = new DomainRecruitment()
        });

        participation.Should().BeEmpty();
    }

    [Fact]
    public void CarryTheMentorOfAGameAsModerator()
    {
        var mentor = Master();

        var participation = MapperFor(Identities.User(mentor.UserId)).ParticipationOf(new DomainGame
        {
            Master = Master(),
            Mentor = mentor,
            Recruitment = new DomainRecruitment()
        });

        participation.Should().Equal(GameParticipation.Moderator);
    }

    private static GameMapper MapperFor(IIdentity identity)
    {
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(identity);
        return new GameMapper(new AttributeSchemaMapper(), identityProvider);
    }

    private static GeneralUser Master() => new()
    {
        UserId = Guid.NewGuid(),
        Username = "gm",
        Role = UserRole.RegularUser
    };

    /// <summary>
    /// The set filters fold to null when absent or empty: the domain reads
    /// null as "no filter", and an empty set must not become "match nothing".
    /// </summary>
    [Fact]
    public void FoldEmptyQuerySetsToNull()
    {
        var query = CreateMapper().ToGamesQuery(new GamesQuery
        {
            Statuses = [],
            HostUsernames = [" ", ""]
        });

        query.Statuses.Should().BeNull();
        query.OwnerUsernames.Should().BeNull("whitespace-only usernames are not a filter");
        query.RequiredTags.Should().BeNull();
    }

    /// <summary>
    /// The description of a tag reaches the public catalog as the hint surface,
    /// which the server renders like every text: the client puts nothing but
    /// server markup into the page, the [tip] of a description included.
    /// </summary>
    [Fact]
    public void CarryATagDescriptionAsTheHint()
    {
        var tag = CreateMapper().ToTag(new DomainGameTag
        {
            ShortId = 63,
            Title = "ERP",
            GroupTitle = "Деликатный контент",
            Description = "[tip img=\"/images/erp-tooltip.gif\"]эротические сцены[/tip]"
        });

        tag.Description.Should().BeOfType<HintBbText>();
        tag.Description!.Value.Should().Be("[tip img=\"/images/erp-tooltip.gif\"]эротические сцены[/tip]");
        tag.Description.Surface.Should().Be(BbSurface.Hint);
    }
}
