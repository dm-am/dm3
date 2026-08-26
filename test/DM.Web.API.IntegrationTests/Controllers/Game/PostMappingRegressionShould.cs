using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using AwesomeAssertions;
using Xunit;
using DomainPost = DM.Domain.Game.Features.Games.Post;
using DomainGame = DM.Domain.Game.Features.Games.Game;
using GameRecruitment = DM.Domain.Game.Features.Games.GameRecruitment;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Regression guard for the "missing game link on home page featured posts"
/// bug. The home-page breadcrumb is gated on `post.room.game` being present
/// end-to-end, so a silent failure anywhere in the mapping chain (domain Post
/// from repository → ApiPost via PostMapper → JSON) would hide the link.
///
/// This test exercises the full mapping pipeline with the same mappers the
/// production DI composes, then asserts the resulting ApiPost has
/// <c>Room.Game</c> populated with Id / PublicId / Title.
///
/// If this test ever fails, the "Best post of the week" and "Latest featured
/// post" widgets on HomePage will stop rendering the game breadcrumb.
/// </summary>
public class PostMappingRegressionShould
{
    private readonly PostMapper _mapper;

    public PostMappingRegressionShould()
    {
        // GameMapper folds Participation off the current identity, which is
        // DI-constructed in production. This pure-mapping test supplies a
        // lightweight guest identity - enough for the mapping to exercise
        // without bringing in the full host.
        var userMapper = new UserMapper(new StubImgproxyUrlBuilder());
        _mapper = new PostMapper(
            new CharacterMapper(userMapper),
            new GameMapper(
                new DM.Web.API.Features.Game.AttributeSchemas.AttributeSchemaMapper(),
                new StubIdentityProvider()));
    }

    /// <summary>
    /// Minimal IIdentityProvider stub returning a Guest (unauthenticated)
    /// identity so that GameRef.Participation evaluates to an empty list
    /// during the mapping test. No side effects, no DI container.
    /// </summary>
    private sealed class StubIdentityProvider : DM.Domain.Core.Identity.IIdentityProvider
    {
        public DM.Domain.Core.Identity.IIdentity Current { get; } =
            DM.Domain.Account.Features.Authentication.Identity.Guest();
    }

    private sealed class StubImgproxyUrlBuilder : DM.Domain.Core.Uploads.IImgproxyUrlBuilder
    {
        public string BuildSquareThumbnail(string sourceObjectKey, int size) => string.Empty;
    }

    [Fact]
    public void PopulateRoomGame_ForRatedPostsProjection()
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
            // RoomRef carries a nested Domain Game (full sidebar-tier
            // payload) instead of scalar id/title fields. The mapping chain
            // DtoGame → GameRef handles everything transitively, and the
            // mapping must preserve publicId + title for the featured-post
            // breadcrumb.
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
            "post.room.game gates the breadcrumb v-if in GamePost.vue; " +
            "losing it breaks the home page featured-post widgets");
        dest.Room.Game!.Id.Should().Be(gameId);
        dest.Room.Game.PublicId.Should().Be("game-public-id");
        dest.Room.Game.Title.Should().Be("Game Title");
    }

    [Fact]
    public void LeaveRoomGameNull_WhenDomainPostHasNoRoom()
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

        // When Room is null on the source, destination Room is also null —
        // the frontend's hasNavigation computed then falls through to the
        // `enrichedGame` branch.
        dest.Room.Should().BeNull();
    }
}
