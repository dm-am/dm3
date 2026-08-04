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
    /// Attaching a file to a post needs the right to post in its room.
    /// </summary>
    /// <remarks>
    /// The same intention and the same room projection PostService.CreateAsync
    /// uses, so a reader of a room cannot attach anything to what they can read,
    /// and someone outside the game cannot reach a post at all: both the post
    /// lookup and the room lookup are already scoped by the caller's id.
    ///
    /// Deliberately not the narrower "author of this post": the product has no
    /// attachment flow yet, and the master editing a post in their own room is a
    /// right the post endpoints already grant. Narrowing it is a product decision
    /// and belongs with the flow that will use it.
    /// </remarks>
    public async Task EnsureAllowedAsync(Guid targetId)
    {
        var userId = _identityProvider.Current.User.UserId;

        var post = await _postRepository.Get(targetId, userId);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        var room = await _roomRepository.GetForUpdate(post.RoomId, userId);
        if (room == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.RoomNotFound);
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.CreatePost, room);
    }
}
