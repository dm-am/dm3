using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Forum.Tests.Authorization;

public class TopicIntentionResolverShould
{
    private readonly TopicIntentionResolver resolver = new();

    [Theory]
    [InlineData(TopicIntention.Like)]
    [InlineData(TopicIntention.CreateComment)]
    [InlineData(TopicIntention.Edit)]
    [InlineData(TopicIntention.Close)]
    public void ForbidEverythingForGuest(TopicIntention intention)
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, intention, new Topic()).Should().BeFalse();
    }

    [Fact]
    public void ForbidCreateCommentInClosedTopic()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.RegularUser).Please(),
            TopicIntention.CreateComment,
            new Topic
            {
                IsClosed = true
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void ForbidCreateCommentUnderTheOrdinaryBan()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.RegularUser).WithAccessPolicy(AccessPolicy.DemocraticBan).Please(),
            TopicIntention.CreateComment,
            new Topic { IsClosed = false });

        // Forum discussion is public speech and there is no own-space exemption
        // on the forum
        actual.Should().BeFalse();
    }

    [Fact]
    public void AllowCreateCommentInOpenTopic()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.RegularUser).Please(),
            TopicIntention.CreateComment,
            new Topic
            {
                IsClosed = false
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void ForbidEditWhenUserNotAuthorAndNotLocalModeratorAndNotModerator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Mentor).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void AllowEditWhenUserIsModerator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Moderator).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowEditWhenUserIsAuthor()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.Moderator).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User(userId).Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowEditWhenUserIsLocalModerator()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.Moderator).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { userId, Guid.NewGuid() }
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void AllowEditWhenUserIsAdministrator()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            TopicIntention.Edit,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            });
        actual.Should().BeTrue();
    }

    [Fact]
    public void ForbidLikeWhenUserIsAuthor()
    {
        var userId = Guid.NewGuid();
        var actual = resolver.IsAllowed(
            Create.User(userId).WithRole(UserRole.Admin).Please(),
            TopicIntention.Like,
            new Topic
            {
                Author = Create.User(userId).Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            });
        actual.Should().BeFalse();
    }

    [Fact]
    public void ForbidLikeWhenUserIsNotAuthor()
    {
        var actual = resolver.IsAllowed(
            Create.User().WithRole(UserRole.Admin).Please(),
            TopicIntention.Like,
            new Topic
            {
                Author = Create.User().Please(),
                IsClosed = false,
                Board = new Board
                {
                    ModeratorIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            });
        actual.Should().BeTrue();
    }

    /// <summary>
    /// Who closes and reopens a topic. The author does, their own, and reopens
    /// only what they closed: a topic a moderator closed stays closed to them. The
    /// board's moderators and the administrators do, any topic, whoever closed it.
    /// A moderator of the site who moderates another board does not: that right
    /// was never theirs, and letting authors in changed nobody else's.
    /// </summary>
    [Theory]
    [InlineData("author", UserRole.RegularUser, false, false, true)]
    [InlineData("author", UserRole.RegularUser, true, true, true)]
    [InlineData("author", UserRole.RegularUser, true, false, false)]
    [InlineData("board moderator", UserRole.Moderator, false, false, true)]
    [InlineData("board moderator", UserRole.Moderator, true, true, true)]
    [InlineData("board moderator", UserRole.Moderator, true, false, true)]
    [InlineData("administrator", UserRole.Admin, true, false, true)]
    [InlineData("stranger", UserRole.RegularUser, false, false, false)]
    [InlineData("site moderator", UserRole.Moderator, false, false, false)]
    [InlineData("site moderator", UserRole.SeniorModerator, true, false, false)]
    public void AllowClosingToTheAuthorAndToWhoAdministratesTheBoard(
        string who, UserRole role, bool isClosed, bool closedByAuthor, bool expected)
    {
        var userId = Guid.NewGuid();
        var topic = TopicOf(
            author: who == "author" ? userId : Guid.NewGuid(),
            boardModerator: who == "board moderator" ? userId : Guid.NewGuid(),
            isClosed: isClosed,
            closedByAuthor: closedByAuthor);

        resolver.IsAllowed(Create.User(userId).WithRole(role).Please(), TopicIntention.Close, topic)
            .Should().Be(expected, who);
    }

    /// <summary>
    /// Closing your own topic is speech like editing it, and an ordinary ban takes
    /// both away. The board moderator keeps the tool under a ban: moderation is
    /// not speech.
    /// </summary>
    [Fact]
    public void TakeClosingFromABannedAuthorButNotFromABannedBoardModerator()
    {
        var authorId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var topic = TopicOf(author: authorId, boardModerator: moderatorId, isClosed: false);

        resolver.IsAllowed(
                Create.User(authorId).WithRole(UserRole.RegularUser)
                    .WithAccessPolicy(AccessPolicy.DemocraticBan).Please(),
                TopicIntention.Close, topic)
            .Should().BeFalse();
        resolver.IsAllowed(
                Create.User(moderatorId).WithRole(UserRole.Moderator)
                    .WithAccessPolicy(AccessPolicy.DemocraticBan).Please(),
                TopicIntention.Close, topic)
            .Should().BeTrue();
    }

    /// <summary>
    /// Whoever may reopen a closed topic may edit it once it is open.
    /// </summary>
    /// <remarks>
    /// TopicService leans on this: a request that reopens a topic is not refused
    /// for the topic being closed, because judged against the closed topic the
    /// author's reopening would be refused by the very state it undoes. If the two
    /// rules ever part, the service would let somebody edit a topic they may only
    /// reopen, and this is where that shows first.
    /// </remarks>
    [Theory]
    [InlineData("author", UserRole.RegularUser)]
    [InlineData("board moderator", UserRole.Moderator)]
    [InlineData("administrator", UserRole.Admin)]
    public void LetWhoeverReopensATopicEditItOnceOpen(string who, UserRole role)
    {
        var userId = Guid.NewGuid();
        var author = who == "author" ? userId : Guid.NewGuid();
        var moderator = who == "board moderator" ? userId : Guid.NewGuid();
        var user = Create.User(userId).WithRole(role).Please();

        resolver.IsAllowed(user, TopicIntention.Close,
                TopicOf(author, moderator, isClosed: true, closedByAuthor: who == "author"))
            .Should().BeTrue();
        resolver.IsAllowed(user, TopicIntention.Edit, TopicOf(author, moderator, isClosed: false))
            .Should().BeTrue(who);
    }

    /// <summary>
    /// The author edits a topic they closed themselves as if it were open, and a
    /// topic somebody who moderates the board closed stays shut to their edits.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void LetTheAuthorEditATopicOnlyTheyClosed(bool closedByAuthor, bool expected)
    {
        var authorId = Guid.NewGuid();
        var author = Create.User(authorId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(author, TopicIntention.Edit,
                TopicOf(authorId, Guid.NewGuid(), isClosed: true, closedByAuthor: closedByAuthor))
            .Should().Be(expected);
    }

    private static Topic TopicOf(Guid author, Guid boardModerator, bool isClosed, bool closedByAuthor = false) => new()
    {
        Author = Create.User(author).Please(),
        IsClosed = isClosed,
        ClosedByAuthor = closedByAuthor,
        Board = new Board { ModeratorIds = new[] { boardModerator } }
    };
}
