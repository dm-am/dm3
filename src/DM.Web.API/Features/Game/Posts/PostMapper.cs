using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Game.Authorization;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.General.Upload;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using ApiRoom = DM.Web.API.Features.Game.Rooms.Room;
using DomainPost = DM.Domain.Game.Features.Posts.Post;
using DomainRoomRef = DM.Domain.Game.Features.Rooms.RoomRef;
using DomainPostAttachment = DM.Domain.Game.Features.Posts.PostAttachment;
using DomainPostEdit = DM.Domain.Game.Features.Posts.PostEdit;
using DomainDiceRoll = DM.Domain.Game.Features.Posts.DiceRoll;
using DomainDiceRollResult = DM.Domain.Game.Features.Posts.DiceRollResult;
using DomainCreatePost = DM.Domain.Game.Features.Posts.CreatePost;
using DomainCreatePostDiceRoll = DM.Domain.Game.Features.Posts.CreatePostDiceRoll;
using DomainUpdatePost = DM.Domain.Game.Features.Posts.UpdatePost;
using PostAttachmentVisibility = DM.Domain.Game.Features.Posts.PostAttachmentVisibility;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Compile-time mapper for game post models. post.room.game renders through the
/// composed <see cref="GameMapper"/>, so rated-post listings carry the game without
/// a second round-trip.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
[UseStaticMapper(typeof(UserRefMappers))]
public partial class PostMapper
{
    [UseMapper]
    private readonly CharacterMapper _characterMapper;

    [UseMapper]
    private readonly GameMapper _gameMapper;

    private readonly IPrivateBlockReading _privateBlockReading;

    private readonly IAuthorizationContextProvider _readers;

    public PostMapper(
        CharacterMapper characterMapper,
        GameMapper gameMapper,
        IPrivateBlockReading privateBlockReading,
        IAuthorizationContextProvider readers)
    {
        _characterMapper = characterMapper;
        _gameMapper = gameMapper;
        _privateBlockReading = privateBlockReading;
        _readers = readers;
    }

    /// <summary>
    /// Domain post to its response DTO, with the envelopes the [private]/[mod]
    /// filtering reads, and without the attachments the post keeps from the
    /// reader (<see cref="PostAttachmentVisibility"/>).
    /// </summary>
    public Post ToPost(DomainPost post)
    {
        var result = ToPostCore(post);

        // The reader the texts of the response are rendered for (BbConverterFactory): one answer
        // for the strip under the post and for the pictures and links in its texts.
        var hidden = post.Attachments.Count > 0
            ? PostAttachmentVisibility.HiddenFrom(_privateBlockReading, _readers.CurrentSubject, post)
            : new HashSet<Guid>();
        EnvelopeTexts(result, post, hidden);

        if (hidden.Count > 0)
        {
            result.Attachments = result.Attachments.Where(a => !hidden.Contains(a.Id)).ToArray();
        }

        return result;
    }

    /// <summary>
    /// Populates the render-context envelope on both BbText fields so the
    /// JSON converter can apply permission-aware filtering. GameText carries
    /// the GamePost surface with the full private-addressee context;
    /// MetagameText is OOC commentary - Comment surface (allows [mod] for
    /// moderators, never [private]). The private-addressee context is the one
    /// the attachments are decided by (DomainPost.GameTextPrivateBlocks), and
    /// both texts carry the files the post keeps from the viewer, whose pictures
    /// and links the render cuts out.
    /// </summary>
    private static void EnvelopeTexts(Post result, DomainPost post, IReadOnlySet<Guid> withheldUploadIds)
    {
        var blocks = post.GameTextPrivateBlocks();
        var snapshot = PrivateAddresseeSnapshot.Read(blocks.PrivateAddresseeSnapshotJson);
        if (result.GameText is not null)
        {
            result.GameText.Context = new RenderContextEnvelope
            {
                Surface = BbSurface.GamePost,
                PostAuthorUserId = blocks.AuthorUserId,
                EditableBy = intentions => intentions.IsAllowed(PostIntention.EditText, post),
                GameId = post.GameId,
                PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
                PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute,
                GameLeadUserIds = blocks.GameLeadUserIds,
                GameShowPrivateMessages = blocks.GameShowPrivateMessages,
                RoomViewPrivateText = blocks.RoomViewPrivateText,
                WithheldUploadIds = withheldUploadIds
            };
        }

        if (result.MetagameText is not null)
        {
            result.MetagameText.Context = new RenderContextEnvelope
            {
                Surface = BbSurface.Comment,
                PostAuthorUserId = post.AuthorUserId,
                EditableBy = intentions => intentions.IsAllowed(PostIntention.EditText, post),
                GameId = post.GameId,
                // [private] stays off this surface; the files kept from the viewer do not.
                WithheldUploadIds = withheldUploadIds
            };
        }
    }

    /// <summary>
    /// Create request to the write model. RoomId comes from the route, the
    /// service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainCreatePost.RoomId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreatePost ToCreatePost(CreatePostRequest request);

    /// <summary>
    /// Update request to the write model. PostId comes from the route, the
    /// service sets it; the request has no character field at all, so an
    /// edit cannot detach the character by construction.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdatePost.PostId))]
    [MapperIgnoreTarget(nameof(DomainUpdatePost.CharacterId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdatePost ToUpdatePost(UpdatePostRequest request);

    // The viewer is composed by PostApiService for a page of posts at once.
    [MapperIgnoreTarget(nameof(Post.Viewer))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Post ToPostCore(DomainPost post);

    // A post's room carries none of the administration of /rooms, and a room
    // reached through a post is one the reader reads, so CanView stays true.
    [MapperIgnoreTarget(nameof(ApiRoom.Access))]
    [MapperIgnoreTarget(nameof(ApiRoom.Type))]
    [MapperIgnoreTarget(nameof(ApiRoom.Accesses))]
    [MapperIgnoreTarget(nameof(ApiRoom.IsArchived))]
    [MapperIgnoreTarget(nameof(ApiRoom.Pendencies))]
    [MapperIgnoreTarget(nameof(ApiRoom.UnreadPostsCount))]
    [MapperIgnoreTarget(nameof(ApiRoom.TotalPostsCount))]
    [MapperIgnoreTarget(nameof(ApiRoom.CharacterNames))]
    [MapperIgnoreTarget(nameof(ApiRoom.CanView))]
    [MapperIgnoreTarget(nameof(ApiRoom.Settings))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ApiRoom ToRoom(DomainRoomRef room);

    [MapProperty(nameof(DomainDiceRoll.DiceCount), nameof(DiceRoll.Rolls))]
    [MapProperty(nameof(DomainDiceRoll.EdgesCount), nameof(DiceRoll.Edges))]
    [MapProperty(nameof(DomainDiceRoll.ExplosionCount), nameof(DiceRoll.Explosion))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DiceRoll ToDiceRoll(DomainDiceRoll roll);

    [MapProperty(nameof(DomainDiceRollResult.IsCritical), nameof(DiceResult.Critical))]
    [MapProperty(nameof(DomainDiceRollResult.IsExploded), nameof(DiceResult.Exploded))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DiceResult ToDiceResult(DomainDiceRollResult result);

    // The domain's XdY vocabulary, "public" inverted into "hidden".
    private static DomainCreatePostDiceRoll ToCreatePostDiceRoll(CreatePostDiceRoll roll) => new()
    {
        EdgesCount = roll.Dice,
        DiceCount = roll.Count,
        Bonus = roll.Bonus,
        ExplosionCount = roll.Explosion,
        IsHidden = !roll.Public,
        Comment = roll.Comment
    };

    // Only the address is composed; the domain model carries no object key. Who sees a marked file
    // goes out with it: ToPost drops every file the viewer does not see.
    private static PostAttachment ToPostAttachment(DomainPostAttachment attachment)
    {
        var addressees = PrivateAddresseeSnapshot.ReadList(attachment.AddresseesJson);
        return new PostAttachment
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            Width = attachment.Width,
            Height = attachment.Height,
            CreatedUtc = attachment.CreatedUtc,
            Url = UploadContentRoute.For(attachment.Id),
            Recipients = addressees == null ? null : PrivateRecipients.Line(addressees.Names),
            Addressees = addressees?.Characters
                .Select(c => new UploadAddressee { Id = c.CharacterId, Name = c.CharacterName }).ToArray()
        };
    }

    private static PostEditInfo ToPostEditInfo(DomainPostEdit edit) => new()
    {
        Id = edit.Id,
        ModifiedUtc = edit.ModifiedUtc,
        Editor = edit.Editor == null ? null! : UserRefMappers.ToUserRef(edit.Editor)
    };
}
