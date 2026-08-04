using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
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
using DM.Web.API.Features.Game.Comments;
using FluentAssertions;
using Moq;
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

    private readonly Mock<IUserBlacklistChecker> _blacklist;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IMapper> _mapper;

    public CommentBlacklistShould()
    {
        _blacklist = Mock<IUserBlacklistChecker>();
        _blacklist
            .Setup(c => c.GetBlockedUserIdsIfFlagEnabledAsync(
                _userId, UserBlacklistSettings.HideComments, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { _blockedId });

        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_userId));

        _mapper = Mock<IMapper>();
    }

    private static (IEnumerable<DomainComment> Comments, PagingResult Paging) NoComments =>
        (Enumerable.Empty<DomainComment>(), PagingResult.Empty(20));

    [Fact]
    public async Task ExcludeBlockedAuthorsFromABlogDiscussion()
    {
        var commentService = Mock<IBlogCommentService>();
        commentService
            .Setup(s => s.GetAsync(_entityId, It.IsAny<BlogCommentsQuery>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(NoComments);

        var service = new BlogCommentApiService(
            commentService.Object, _identityProvider.Object, _blacklist.Object, _mapper.Object);

        await service.Get(_entityId, new BlogCommentsQuery());

        commentService.Verify(s => s.GetAsync(_entityId, It.IsAny<BlogCommentsQuery>(),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId))), Times.Once);
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAPublicationDiscussion()
    {
        var commentService = Mock<IPublicationCommentService>();
        commentService
            .Setup(s => s.GetAsync(_entityId, It.IsAny<PublicationCommentsQuery>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(NoComments);

        var service = new PublicationCommentApiService(
            commentService.Object, _identityProvider.Object, _blacklist.Object, _mapper.Object);

        await service.Get(_entityId, new PublicationCommentsQuery());

        commentService.Verify(s => s.GetAsync(_entityId, It.IsAny<PublicationCommentsQuery>(),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId))), Times.Once);
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAPublicationCommentList()
    {
        var commentService = Mock<IPublicationCommentService>();
        commentService
            .Setup(s => s.GetAsync(_entityId, It.IsAny<PublicationCommentsQuery>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(NoComments);

        var service = new PublicationCommentApiService(
            commentService.Object, _identityProvider.Object, _blacklist.Object, _mapper.Object);

        await service.Get(_entityId, new PublicationCommentsQuery());

        commentService.Verify(s => s.GetAsync(_entityId, It.IsAny<PublicationCommentsQuery>(),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId))), Times.Once);
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAForumDiscussion()
    {
        var commentService = Mock<ITopicCommentService>();
        commentService
            .Setup(s => s.GetAsync(_entityId, It.IsAny<CommentsQuery>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(NoComments);

        var service = new ForumCommentApiService(
            commentService.Object, _identityProvider.Object, _blacklist.Object, _mapper.Object);

        await service.GetDiscussion(_entityId, new PagingQuery());

        commentService.Verify(s => s.GetAsync(_entityId, It.IsAny<CommentsQuery>(),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId))), Times.Once);
    }

    [Fact]
    public async Task ExcludeBlockedAuthorsFromAGameDiscussion()
    {
        var commentService = Mock<IGameCommentService>();
        commentService
            .Setup(s => s.GetAsync(_entityId, It.IsAny<GameCommentsQuery>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(NoComments);

        var service = new GameCommentApiService(
            commentService.Object, _identityProvider.Object, _blacklist.Object, _mapper.Object);

        await service.Get(_entityId, new GameCommentsQuery());

        commentService.Verify(s => s.GetAsync(_entityId, It.IsAny<GameCommentsQuery>(),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(_blockedId))), Times.Once);
    }
}
