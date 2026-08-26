using System;
using DM.Domain.Core.Comments;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Uploads;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Features.Blog.Comments;
using DM.Web.API.Features.Blog.PublicationComments;
using DM.Web.API.Features.Forum.Comments;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Comments;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainComment = DM.Domain.Core.Comments.Comment;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// A blocked author stays blocked on every comment surface.
/// </summary>
/// <remarks>
/// Hiding the comments of a blacklisted author is one rule, but the reading of a
/// discussion is written once per surface, and the exclusion has to be re-added by
/// hand in each copy. Three copies carried it and the fourth — publications — did
/// not, so the same setting hid the same author under a blog, a topic and a game
/// and left him visible under a publication. Nothing failed: the endpoint answered
/// 200 with one comment more than the reader had asked to see.
///
/// The assertion is on the call into the domain rather than on the response body,
/// because the filter is applied in the query and a service that never passes the
/// ids produces a correct-looking response over the wrong rows.
/// </remarks>
public class CommentBlacklistShould : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _blockedId = Guid.NewGuid();
    private readonly Guid _entityId = Guid.NewGuid();

    private readonly IUserBlacklistChecker _blacklist;
    private readonly IIdentityProvider _identityProvider;
    private readonly CommentMapper _commentMapper;
    private readonly DiscussionMapper _discussionMapper;

    public CommentBlacklistShould()
    {
        _blacklist = Mock<IUserBlacklistChecker>();
        _blacklist
            .GetBlockedUserIdsIfFlagEnabledAsync(
                _userId, UserBlacklistSettings.HideComments, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { _blockedId });

        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_userId));

        // Real mappers: nothing is mapped over the empty pages below, but the
        // constructors still have to be answered for.
        var userMapper = new UserMapper(Mock<IImgproxyUrlBuilder>());
        _commentMapper = new CommentMapper(userMapper);
        _discussionMapper = new DiscussionMapper(userMapper);
    }

    private static (IEnumerable<DomainComment> Comments, PagingResult Paging) NoComments =>
        (Enumerable.Empty<DomainComment>(), PagingResult.Empty(20));

    [Fact]
    public async Task ExcludeBlockedAuthorsFromABlogDiscussion()
    {
        var commentService = Mock<IBlogCommentService>();
        commentService
            .GetAsync(_entityId, Arg.Any<CommentsQuery>(),
                Arg.Any<IReadOnlyCollection<Guid>>()).Returns(NoComments);

        var service = new BlogCommentApiService(
            commentService, _identityProvider, _blacklist, _commentMapper,
            Mock<IQuoteSourceService>());

        await service.Get(_entityId, new CommentsQuery());

        await commentService.Received(1).GetAsync(_entityId, Arg.Any<CommentsQuery>(),
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId)));
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAPublicationDiscussion()
    {
        var commentService = Mock<IPublicationCommentService>();
        commentService
            .GetAsync(_entityId, Arg.Any<CommentsQuery>(),
                Arg.Any<IReadOnlyCollection<Guid>>()).Returns(NoComments);

        var service = new PublicationCommentApiService(
            commentService, _identityProvider, _blacklist, _commentMapper,
            Mock<IQuoteSourceService>());

        await service.Get(_entityId, new CommentsQuery());

        await commentService.Received(1).GetAsync(_entityId, Arg.Any<CommentsQuery>(),
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId)));
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAPublicationCommentList()
    {
        var commentService = Mock<IPublicationCommentService>();
        commentService
            .GetAsync(_entityId, Arg.Any<CommentsQuery>(),
                Arg.Any<IReadOnlyCollection<Guid>>()).Returns(NoComments);

        var service = new PublicationCommentApiService(
            commentService, _identityProvider, _blacklist, _commentMapper,
            Mock<IQuoteSourceService>());

        await service.Get(_entityId, new CommentsQuery());

        await commentService.Received(1).GetAsync(_entityId, Arg.Any<CommentsQuery>(),
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId)));
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAForumDiscussion()
    {
        var commentService = Mock<ITopicCommentService>();
        commentService
            .GetAsync(_entityId, Arg.Any<CommentsQuery>(),
                Arg.Any<IReadOnlyCollection<Guid>>()).Returns(NoComments);

        var service = new ForumCommentApiService(
            commentService, _identityProvider, _blacklist,
            _discussionMapper, new ForumCommentMapper());

        await service.GetDiscussion(_entityId, new PagingQuery());

        await commentService.Received(1).GetAsync(_entityId, Arg.Any<CommentsQuery>(),
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId)));
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAGameDiscussion()
    {
        var commentService = Mock<IGameCommentService>();
        commentService
            .GetAsync(_entityId, Arg.Any<CommentsQuery>(),
                Arg.Any<IReadOnlyCollection<Guid>>()).Returns(NoComments);

        var service = new GameCommentApiService(
            commentService, _identityProvider, _blacklist, _commentMapper,
            Mock<IQuoteSourceService>());

        await service.Get(_entityId, new CommentsQuery());

        await commentService.Received(1).GetAsync(_entityId, Arg.Any<CommentsQuery>(),
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId)));
    }
}
