using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Testing;
using DM.Testing.Dsl;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

/// <summary>
/// Who may put a file on a post, read one off it, and take one off it.
/// </summary>
/// <remarks>
/// Three different answers to what looks like one question, and the reason they
/// differ is the point of this file. Attaching is authorship, so it is the author
/// alone. Reading is visibility, so it is whoever may read the post. Removing is
/// editing what a room contains, so it follows the post's edit rule and reaches
/// the master.
///
/// Existence is never a separate check: every path goes through
/// PostRepository.Get with the caller's own id, which is the read that applies
/// the room filter, so an invisible post is absent rather than refused.
/// </remarks>
public class PostAttachmentUploadAuthorizerShould : UnitTestBase
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();

    private readonly Mock<IPostRepository> _postRepository;
    private readonly Mock<IRoomRepository> _roomRepository;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly PostAttachmentUploadAuthorizer _sut;

    public PostAttachmentUploadAuthorizerShould()
    {
        _postRepository = Mock<IPostRepository>();
        _roomRepository = Mock<IRoomRepository>();
        _intentionManager = Mock<IIntentionManager>();

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId));

        _sut = new PostAttachmentUploadAuthorizer(
            _postRepository.Object,
            _roomRepository.Object,
            _intentionManager.Object,
            identityProvider.Object);
    }

    /// <summary>A post the caller can see, written by whoever is named.</summary>
    private void PostVisible(Guid authorId) =>
        _postRepository.Setup(r => r.Get(_postId, _currentUserId))
            .ReturnsAsync(new Post
            {
                Id = _postId,
                RoomId = _roomId,
                Author = new GeneralUser { UserId = authorId, Username = "author" },
                AuthorUserId = authorId,
                GameText = "текст",
                MetagameText = string.Empty,
            });

    /// <summary>A post outside the caller's scope: the read answers with nothing.</summary>
    private void PostInvisible() =>
        _postRepository.Setup(r => r.Get(_postId, _currentUserId))
            .ReturnsAsync((Post?)null);

    private void RoomAvailable() =>
        _roomRepository.Setup(r => r.GetForUpdate(_roomId, _currentUserId))
            .ReturnsAsync(new RoomToUpdate { Id = _roomId });

    // ─────────────────────────────────────────────────────────────────────────
    // Attaching
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LetTheAuthorAttachAFileToTheirOwnPost()
    {
        PostVisible(authorId: _currentUserId);

        var act = () => _sut.EnsureAllowedAsync(_postId);

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The rule this narrows. Being allowed to write in the room is what creates
    /// a post of one's own; it is not permission to add material to somebody
    /// else's, which would publish a file under their name.
    /// </summary>
    [Fact]
    public async Task RefuseAnybodyElseAttachingToAPostTheyDidNotWrite()
    {
        PostVisible(authorId: Guid.NewGuid());

        var act = () => _sut.EnsureAllowedAsync(_postId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The room's write permission is never consulted, so the master of the game
    /// is refused here exactly as any other non-author is.
    /// </summary>
    [Fact]
    public async Task NeverAskWhetherTheCallerMayPostInTheRoom()
    {
        PostVisible(authorId: Guid.NewGuid());

        try
        {
            await _sut.EnsureAllowedAsync(_postId);
        }
        catch (HttpException)
        {
            // expected
        }

        _roomRepository.Verify(r => r.GetForUpdate(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        _intentionManager.Verify(
            m => m.ThrowIfForbidden(It.IsAny<RoomIntention>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task AnswerNotFoundForAPostTheCallerCannotSee()
    {
        PostInvisible();

        var act = () => _sut.EnsureAllowedAsync(_postId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reading
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LetAnyReaderOfThePostReadItsAttachment()
    {
        PostVisible(authorId: Guid.NewGuid());

        var act = () => _sut.EnsureReadAllowedAsync(_postId);

        await act.Should().NotThrowAsync(
            "the file is visible wherever the post is, which is not only to its author");
    }

    /// <summary>
    /// 404, not 403. A refusal that distinguishes "no such file" from "not for
    /// you" tells an outsider that a private game has one.
    /// </summary>
    [Fact]
    public async Task AnswerNotFoundWhenReadingAnAttachmentOfAnInvisiblePost()
    {
        PostInvisible();

        var act = () => _sut.EnsureReadAllowedAsync(_postId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The read is scoped to the caller. Passing anything else — an empty id, a
    /// system identity — would make every closed room readable through this one
    /// method while every other read of the same post stayed shut.
    /// </summary>
    [Fact]
    public async Task ReadThePostInTheCallersOwnScope()
    {
        PostVisible(authorId: _currentUserId);

        await _sut.EnsureReadAllowedAsync(_postId);

        _postRepository.Verify(r => r.Get(_postId, _currentUserId), Times.Once);
        _postRepository.Verify(
            r => r.Get(It.IsAny<Guid>(), It.Is<Guid>(id => id != _currentUserId)), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Removing
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LetWhoeverMayEditThePostTakeAFileOffIt()
    {
        PostVisible(authorId: Guid.NewGuid());
        RoomAvailable();
        _intentionManager
            .Setup(m => m.IsAllowed(PostIntention.EditText, It.IsAny<(Post, RoomToUpdate)>()))
            .Returns(true);

        (await _sut.MayDetachAsync(_postId)).Should().BeTrue();
    }

    [Fact]
    public async Task GrantNoRemovalRightWhenThePostMayNotBeEdited()
    {
        PostVisible(authorId: Guid.NewGuid());
        RoomAvailable();
        _intentionManager
            .Setup(m => m.IsAllowed(PostIntention.EditText, It.IsAny<(Post, RoomToUpdate)>()))
            .Returns(false);

        (await _sut.MayDetachAsync(_postId)).Should().BeFalse();
    }

    [Fact]
    public async Task GrantNoRemovalRightOverAPostTheCallerCannotSee()
    {
        PostInvisible();

        (await _sut.MayDetachAsync(_postId)).Should().BeFalse();
    }
}
