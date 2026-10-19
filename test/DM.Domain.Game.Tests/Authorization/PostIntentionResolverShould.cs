using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Testing.Dsl;
using DM.Domain.Game.Tests.Dsl;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

public class PostIntentionResolverShould
{
    private readonly PostIntentionResolver resolver = new(new PrivateBlockReading(new BbParserProvider()));

    private static Post PostBy(Guid authorId) =>
        new() { Author = new GeneralUser { UserId = authorId } };

    [Fact]
    public void AllowDeleteForAuthor()
    {
        var authorId = Guid.NewGuid();
        var user = Create.User(authorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PostIntention.Delete, PostBy(authorId)).Should().BeTrue();
    }

    /// <summary>
    /// Nothing in a game room is moderated: no rank deletes a post of somebody else.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void ForbidDeleteForModeration(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, PostIntention.Delete, PostBy(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void ForbidDeleteForNonAuthorRegularUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, PostIntention.Delete, PostBy(Guid.NewGuid())).Should().BeFalse();
    }

    /// <summary>
    /// Nor does any rank edit one: the edit of moderation is not a right in a game room.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void ForbidEditTextForModeration(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();
        var room = new RoomToUpdate { Game = new GameBuilder().WithMaster(Guid.NewGuid()).Please() };

        resolver.IsAllowed(user, PostIntention.EditText, (PostBy(Guid.NewGuid()), room))
            .Should().BeFalse();
        resolver.IsAllowed(user, PostIntention.EditText, PostBy(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void AllowEditTextForAuthor()
    {
        var authorId = Guid.NewGuid();
        var user = Create.User(authorId).WithRole(UserRole.RegularUser).Please();
        var room = new RoomToUpdate { Game = new GameBuilder().Please() };

        resolver.IsAllowed(user, PostIntention.EditText, (PostBy(authorId), room))
            .Should().BeTrue();
    }

    [Fact]
    public void ForbidEditTextForUnrelatedRegularUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        // A different master; the actor is neither author, moderator, nor lead.
        var room = new RoomToUpdate { Game = new GameBuilder().WithMaster(Guid.NewGuid()).Please() };

        resolver.IsAllowed(user, PostIntention.EditText, (PostBy(Guid.NewGuid()), room))
            .Should().BeFalse();
    }

    private static readonly Guid AddresseeId = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    /// <summary>A post of somebody else with one hidden block, for the addressee, whose owner is named.</summary>
    private static Post PostWithABlockForVasya(
        string opening = "[private=\"Вася\"]", Guid? masterId = null, bool roomShowsAll = false) => new()
        {
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            AuthorUserId = Guid.NewGuid(),
            GameText = $"Ход. {opening}тайна[/private]",
            PrivateAddresseeSnapshotJson = $"{{\"Вася\":[{{\"owner\":\"{AddresseeId}\"}}]}}",
            GameMasterUserId = masterId ?? Guid.NewGuid(),
            RoomViewPrivateText = roomShowsAll,
        };

    /// <summary>
    /// A moderator is refused a post of somebody else by both doors of the right, a hidden block or none.
    /// </summary>
    [Fact]
    public void ForbidEditTextToAModeratorAHiddenBlockOfThePostIsClosedTo()
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();
        var post = PostWithABlockForVasya();
        var room = new RoomToUpdate { Game = new GameBuilder().WithMaster(post.GameMasterUserId).Please() };

        resolver.IsAllowed(user, PostIntention.EditText, post).Should().BeFalse();
        resolver.IsAllowed(user, PostIntention.EditText, (post, room)).Should().BeFalse();
    }

    [Fact]
    public void ForbidEditTextToAModeratorWhoReadsEveryBlock()
    {
        var addressee = Create.User(AddresseeId).WithRole(UserRole.Moderator).Please();
        var anybody = Create.User().WithRole(UserRole.Moderator).Please();

        resolver.IsAllowed(addressee, PostIntention.EditText, PostWithABlockForVasya())
            .Should().BeFalse("reading the block is no right to edit a post in a room");
        resolver.IsAllowed(anybody, PostIntention.EditText, PostWithABlockForVasya(roomShowsAll: true))
            .Should().BeFalse("nor is a room that shows every addressed block");
    }

    /// <summary>
    /// A file marked "Выбранные" outside every block, and a moderator it is marked for or not: no edit.
    /// </summary>
    [Fact]
    public void ForbidEditTextToAModeratorAMarkedFileOfThePostIsClosedTo()
    {
        var fileId = Guid.NewGuid();
        var post = new Post
        {
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            AuthorUserId = Guid.NewGuid(),
            GameText = $"Карта: [img]https://dm.am/v1/uploads/{fileId:D}/content[/img]",
            GameMasterUserId = Guid.NewGuid(),
            Attachments =
            [
                new PostAttachment
                {
                    Id = fileId,
                    FileName = "karta.png",
                    ContentType = "image/png",
                    AddresseesJson = $"[{{\"character\":\"{Guid.NewGuid()}\",\"name\":\"Вася\",\"owner\":\"{AddresseeId}\"}}]"
                }
            ]
        };
        var room = new RoomToUpdate { Game = new GameBuilder().WithMaster(post.GameMasterUserId).Please() };
        var moderator = Create.User().WithRole(UserRole.Moderator).Please();

        resolver.IsAllowed(moderator, PostIntention.EditText, post).Should().BeFalse();
        resolver.IsAllowed(moderator, PostIntention.EditText, (post, room)).Should().BeFalse();
        resolver.IsAllowed(Create.User(AddresseeId).WithRole(UserRole.Moderator).Please(), PostIntention.EditText, post)
            .Should().BeFalse("the file is marked for him, and the post is still not his");
    }

    [Fact]
    public void AllowEditTextToTheMasterWhateverTheBlocks()
    {
        var masterId = Guid.NewGuid();
        var master = Create.User(masterId).WithRole(UserRole.RegularUser).Please();
        var post = PostWithABlockForVasya("[private]", masterId);
        var room = new RoomToUpdate { Game = new GameBuilder().WithMaster(masterId).Please() };

        resolver.IsAllowed(master, PostIntention.EditText, post).Should().BeTrue();
        resolver.IsAllowed(master, PostIntention.EditText, (post, room)).Should().BeTrue();
    }

    [Fact]
    public void AllowEditTextToTheAuthorWhateverTheBlocks()
    {
        var post = PostWithABlockForVasya("[private]");
        var author = Create.User(post.Author.UserId).WithRole(UserRole.RegularUser).Please();
        post.AuthorUserId = post.Author.UserId;

        resolver.IsAllowed(author, PostIntention.EditText, post).Should().BeTrue();
    }
}
