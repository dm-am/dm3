using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Rooms;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using AwesomeAssertions;
using Xunit;
using DomainPost = DM.Domain.Game.Features.Posts.Post;
using DomainGame = DM.Domain.Game.Features.Games.Game;
using GameRecruitment = DM.Domain.Game.Features.Games.GameRecruitment;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// The full mapping chain the production DI composes keeps <c>Room.Game</c> with Id, PublicId and Title.
/// </summary>
public class PostMappingRegressionShould
{
    private readonly PostMapper _mapper;

    public PostMappingRegressionShould()
    {
        // GameMapper reads Participation off the current identity: a guest one is enough here.
        var userMapper = new UserMapper();
        _mapper = new PostMapper(
            new CharacterMapper(userMapper),
            new GameMapper(
                new DM.Web.API.Features.Game.AttributeSchemas.AttributeSchemaMapper(),
                new StubIdentityProvider()),
            new DM.Infrastructure.Core.Parsing.PrivateBlockReading(
                new DM.Infrastructure.Core.Parsing.BbParserProvider()),
            new StubReaders());
    }

    /// <summary>
    /// A guest identity, so GameRef.Participation evaluates to an empty list.
    /// </summary>
    private sealed class StubIdentityProvider : DM.Domain.Core.Identity.IIdentityProvider
    {
        public DM.Domain.Core.Identity.IIdentity Current { get; } =
            DM.Domain.Account.Features.Authentication.Identity.Guest();
    }

    /// <summary>A guest, as the reader the attachments of a post are decided for.</summary>
    private sealed class StubReaders : DM.Domain.Core.Authorization.IAuthorizationContextProvider
    {
        public DM.Domain.Core.Authorization.IAuthorizationSubject CurrentSubject =>
            DM.Domain.Core.Identity.AuthenticatedUser.Guest;
    }

    [Fact]
    public void PopulateRoomGameForRatedPostsProjection()
    {
        var gameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var roomId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var authorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var src = new DomainPost
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            Author = new GeneralUser
            {
                UserId = authorId,
                Username = "author",
                Role = UserRole.RegularUser,
                Status = "Active"
            },
            GameText = "body",
            MetagameText = "meta",
            CreatedUtc = DateTimeOffset.UtcNow,
            // RoomRef carries a nested domain game, mapped transitively to GameRef.
            Room = new RoomRef
            {
                Id = roomId,
                RoomNumber = 1,
                Title = "Room 1",
                Game = new DomainGame
                {
                    Id = gameId,
                    PublicId = "game-public-id",
                    Title = "Game Title",
                    Master = new GeneralUser
                    {
                        UserId = Guid.NewGuid(),
                        Username = "gm",
                        Role = UserRole.RegularUser,
                        Status = "Active"
                    },
                    Recruitment = new GameRecruitment
                    {
                        IsOpen = false,
                        PcCount = 0
                    }
                }
            }
        };

        var dest = _mapper.ToPost(src);

        dest.Room.Should().NotBeNull(
            "post.room is required for featured-post navigation");
        dest.Room!.Id.Should().Be(roomId);
        dest.Room.Game.Should().NotBeNull(
            "the reader of a featured post is led to its game");
        dest.Room.Game!.Id.Should().Be(gameId);
        dest.Room.Game.PublicId.Should().Be("game-public-id");
        dest.Room.Game.Title.Should().Be("Game Title");
    }

    [Fact]
    public void LeaveRoomGameNullWhenDomainPostHasNoRoom()
    {
        var src = new DomainPost
        {
            Id = Guid.NewGuid(),
            RoomId = Guid.NewGuid(),
            Author = new GeneralUser
            {
                UserId = Guid.NewGuid(),
                Username = "author",
                Role = UserRole.RegularUser,
                Status = "Active"
            },
            GameText = "body",
            MetagameText = "meta",
            CreatedUtc = DateTimeOffset.UtcNow,
            Room = null
        };

        var dest = _mapper.ToPost(src);

        dest.Room.Should().BeNull();
    }
}
