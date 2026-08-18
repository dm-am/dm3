using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class PostAttachmentUploadAuthorizer : IUploadTargetAuthorizer
{
    private readonly IPostRepository _postRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PostAttachmentUploadAuthorizer(
        IPostRepository postRepository,
        IRoomRepository roomRepository,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider)
    {
        _postRepository = postRepository;
        _roomRepository = roomRepository;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public UploadType Type => UploadType.PostAttachment;

    /// <summary>
    /// Only the author of the post attaches files to it.
    /// </summary>
    /// <remarks>
    /// Not "whoever may write in this room", which is the wider rule the endpoint
    /// would otherwise inherit from post creation. An attachment is part of what
    /// the post says, and adding material to somebody else's post puts words under
    /// their name — a master may edit a player's text, which is a visible,
    /// recorded change, but nobody adds a file to a post they did not write.
    ///
    /// The consequences are deliberate and worth naming. A global moderator may
    /// change the text of any post and may not attach anything to it; a master may
    /// attach nothing to a player's post while being able to remove what is
    /// already there. Removal is the opposite question and has its own answer in
    /// MayDetachAsync: taking a file off a post is moderating what is in a room,
    /// putting one on is authorship.
    ///
    /// Existence is decided by the reader's own scope: PostRepository.Get applies
    /// GameAccessibilityFilters.RoomAvailable, so a post in a game the caller
    /// cannot see is absent rather than forbidden.
    /// </remarks>
    public async Task EnsureAllowedAsync(Guid targetId)
    {
        var userId = _identityProvider.Current.User.UserId;

        var post = await _postRepository.Get(targetId, userId);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        if (post.Author.UserId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The attachment is visible exactly where the post is, and the post's
    /// visibility is one expression — GameAccessibilityFilters.RoomAvailable,
    /// applied by the same repository read every other reader of a post goes
    /// through. Restating it here would be a second answer to one question, and
    /// the second answer is the one that drifts.
    /// </remarks>
    public async Task EnsureReadAllowedAsync(Guid targetId)
    {
        var userId = _identityProvider.Current.User.UserId;

        var post = await _postRepository.Get(targetId, userId);
        if (post == null)
        {
            // 404 and not 403: an outsider asking after a file in a closed game
            // learns nothing from the answer, which is the whole point of the
            // prefix being closed in the first place.
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Whoever may edit the post may take a file off it: the author, and the game's
    /// leads under the character's post-edit policy. That is PostIntention.EditText
    /// over the same pair PostService.UpdateAsync asks it about — the file is part
    /// of the post's content, so the right to change the content is the right to
    /// change this.
    ///
    /// Moderator+ is not asked for here; the upload's own delete intention already
    /// carries it, and the caller composes the two.
    /// </remarks>
    public async Task<bool> MayDetachAsync(Guid targetId)
    {
        var userId = _identityProvider.Current.User.UserId;

        var post = await _postRepository.Get(targetId, userId);
        if (post == null)
        {
            return false;
        }

        var room = await _roomRepository.GetForUpdate(post.RoomId, userId);
        if (room == null)
        {
            return false;
        }

        return _intentionManager.IsAllowed(PostIntention.EditText, (post, room));
    }
}
