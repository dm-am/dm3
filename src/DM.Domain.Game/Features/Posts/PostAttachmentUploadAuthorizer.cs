using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
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
    private readonly IPrivateBlockReading _privateBlockReading;
    private readonly IPostAttachmentCarrierRepository _carrierRepository;

    public PostAttachmentUploadAuthorizer(
        IPostRepository postRepository,
        IRoomRepository roomRepository,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IPrivateBlockReading privateBlockReading,
        IPostAttachmentCarrierRepository carrierRepository)
    {
        _postRepository = postRepository;
        _roomRepository = roomRepository;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _privateBlockReading = privateBlockReading;
        _carrierRepository = carrierRepository;
    }

    /// <inheritdoc />
    public UploadType Type => UploadType.PostAttachment;

    /// <summary>
    /// Only the author of the post attaches files to it.
    /// </summary>
    /// <remarks>
    /// Adding a file puts words under the author's name, so nobody adds one to a post he did not write;
    /// removal is moderation and has its own answer in MayDetachAsync. The reader's scope decides existence.
    /// </remarks>
    public async Task EnsureAllowedAsync(Guid targetId)
    {
        var userId = _identityProvider.Current.User.UserId;

        var post = await _postRepository.Get(targetId, userId);
        if (post == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        if (post.Author?.UserId != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Visible where the post is, by the repository read every reader of a post goes through, less a
    /// file its mark or the game text keeps from the caller (PostAttachmentVisibility): that one is as
    /// absent to him as a file of a post he cannot see.
    /// </remarks>
    public async Task EnsureReadAllowedAsync(StoredUpload upload)
    {
        var reader = _identityProvider.Current.User;

        var post = await _postRepository.Get(upload.TargetId!.Value, reader.UserId);
        if (post == null)
        {
            // 404, not 403: an outsider learns nothing about a file in a closed game.
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        }

        if (HiddenFrom(reader, post, [upload]).Count > 0)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UploadNotFound);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The posts are read past the room, since the caller's right passes it, and are asked only what
    /// they keep from him: a moderator outside the game is refused a file marked for others, or shown
    /// in a block he does not read.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads)
    {
        var reader = _identityProvider.Current.User;
        var carriers = await _carrierRepository.GetCarriers(
            uploads.Select(u => u.TargetId!.Value).Distinct().ToArray());

        var withheld = new HashSet<Guid>();
        foreach (var onePost in uploads.GroupBy(u => u.TargetId!.Value))
        {
            if (carriers.TryGetValue(onePost.Key, out var post))
            {
                withheld.UnionWith(HiddenFrom(reader, post, onePost.ToArray()));
            }
            else
            {
                withheld.UnionWith(onePost.Select(u => u.Id));
            }
        }

        return withheld;
    }

    /// <inheritdoc />
    /// <remarks>By the read a guest makes: the post where a guest finds it, less what it keeps from him.</remarks>
    public async Task<bool> OpenToGuestAsync(StoredUpload upload)
    {
        var guest = AuthenticatedUser.Guest;
        var post = await _postRepository.Get(upload.TargetId!.Value, guest.UserId);
        return post != null && HiddenFrom(guest, post, [upload]).Count == 0;
    }

    /// <summary>The files of one post it keeps from the reader.</summary>
    private IReadOnlySet<Guid> HiddenFrom(IAuthorizationSubject reader, Post post, IReadOnlyCollection<StoredUpload> uploads) =>
        PostAttachmentVisibility.HiddenFrom(
            _privateBlockReading, reader, post, uploads.ToDictionary(u => u.Id, u => u.AddresseesJson));

    /// <inheritdoc />
    /// <remarks>
    /// The characters chosen are matched against those let into the room, as the addressees of a block
    /// are, and frozen with their owners. One the mark of the file already names stays chosen after he
    /// left the room, as the mark froze him: a block keeps its addressees so, and the author keeps or
    /// drops him by his box. A file kept for everybody is refused when the game text puts
    /// it only inside hidden blocks: the plate would say "Все" while the text gives the file to the
    /// readers of the blocks, and the author is asked which of the two he means. A new file is
    /// mentioned nowhere yet.
    /// </remarks>
    public async Task<string?> FreezeAudienceAsync(Guid targetId, Guid? uploadId, UploadAudience audience)
    {
        if (audience.VisibleTo == UploadVisibleTo.All && uploadId == null)
        {
            return null;
        }

        var userId = _identityProvider.Current.User.UserId;
        var post = await _postRepository.Get(targetId, userId)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);

        if (audience.VisibleTo == UploadVisibleTo.All)
        {
            var attachment = post.Attachments.FirstOrDefault(a => a.Id == uploadId)
                ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UploadNotFound);
            if (_privateBlockReading.OnlyInsideBlocks(post.GameText, [attachment.Id]).Count > 0)
            {
                throw new HttpException(
                    HttpStatusCode.BadRequest, RefusalMessage.FileForAllOnlyInsideHiddenBlock(attachment.FileName));
            }

            return null;
        }

        var room = await _roomRepository.GetForUpdate(post.RoomId, userId)
            ?? throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.PostNotFound);
        var marked = PrivateAddresseeSnapshot.ReadList(
            post.Attachments.FirstOrDefault(a => a.Id == uploadId)?.AddresseesJson);

        // The room first: a character still let in is frozen as the room has him now.
        return PrivateAddresseeSnapshot.BuildList(
            audience.CharacterIds, room.PrivateAddressees().Concat(marked?.Characters ?? []));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Whoever may edit the post may take a file off it; Moderator+ comes from the upload's own intention.
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
