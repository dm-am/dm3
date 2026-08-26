using DM.Domain.Core.Content;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.General.Upload;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using ApiRoom = DM.Web.API.Features.Game.Rooms.Room;
using DomainPost = DM.Domain.Game.Features.Games.Post;
using DomainRoomRef = DM.Domain.Game.Features.Games.RoomRef;
using DomainPostAttachment = DM.Domain.Game.Features.Games.PostAttachment;
using DomainPostEdit = DM.Domain.Game.Features.Posts.PostEdit;
using DomainDiceRoll = DM.Domain.Game.Features.Posts.DiceRoll;
using DomainDiceRollResult = DM.Domain.Game.Features.Posts.DiceRollResult;
using DomainCreatePost = DM.Domain.Game.Features.Posts.CreatePost;
using DomainCreatePostDiceRoll = DM.Domain.Game.Features.Posts.CreatePostDiceRoll;
using DomainUpdatePost = DM.Domain.Game.Features.Posts.UpdatePost;

namespace DM.Web.API.Features.Game.Posts;

/// <summary>
/// Compile-time mapper for game post models. The Mapperly counterpart of
/// the post profile: the render-context envelope is an explicit step of
/// <see cref="ToPost"/>, and post.room.game renders through the composed
/// <see cref="GameMapper"/> so rated-post listings keep the full
/// sidebar-tier game payload without a second round-trip.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class PostMapper
{
    [UseMapper]
    private readonly CharacterMapper _characterMapper;

    [UseMapper]
    private readonly GameMapper _gameMapper;

    public PostMapper(CharacterMapper characterMapper, GameMapper gameMapper)
    {
        _characterMapper = characterMapper;
        _gameMapper = gameMapper;
    }

    /// <summary>
    /// Domain post to its response DTO. The explicit envelope step replaces
    /// the AutoMapper AfterMap - it must run on every post that crosses the
    /// API, or permission-aware [private]/[mod] filtering silently breaks.
    /// </summary>
    public Post ToPost(DomainPost post)
    {
        var result = ToPostCore(post);
        EnvelopeTexts(result, post);
        return result;
    }

    /// <summary>
    /// Populates the render-context envelope on both BbText fields so the
    /// JSON converter can apply permission-aware filtering. GameText carries
    /// the GamePost surface with the full private-addressee context;
    /// MetagameText is OOC commentary - Comment surface (allows [mod] for
    /// moderators, never [private]).
    /// </summary>
    public static void EnvelopeTexts(Post result, DomainPost post)
    {
        var snapshot = PrivateAddresseeSnapshot.Read(post.PrivateAddresseeSnapshotJson);
        if (result.GameText is not null)
        {
            result.GameText.Context = new RenderContextEnvelope
            {
                Surface = BbSurface.GamePost,
                PostAuthorUserId = post.AuthorUserId,
                GameId = post.GameId,
                PrivateAddresseeOwnerUserIdsByAttribute = snapshot.OwnerUserIdsByAttribute,
                PrivateAddresseeNamesByAttribute = snapshot.AddresseeNamesByAttribute,
                GameLeadUserIds = post.GameLeadUserIds,
                PostSharePrivateWithAll = post.SharePrivateWithAll,
                RoomViewPrivateText = post.RoomViewPrivateText
            };
        }

        if (result.MetagameText is not null)
        {
            result.MetagameText.Context = new RenderContextEnvelope
            {
                Surface = BbSurface.Comment,
                PostAuthorUserId = post.AuthorUserId,
                GameId = post.GameId
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
    [MapperIgnoreTarget(nameof(DomainUpdatePost.IsRemoved))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdatePost ToUpdatePost(UpdatePostRequest request);

    // Texts and their envelopes are composed in the wrapper. The author maps
    // by convention: a post is written by exactly one user, the column is not
    // nullable, and a null-tolerant hop into a non-nullable field would only
    // move the failure from the query to the response.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Post ToPostCore(DomainPost post);

    // Fields that only exist on the dedicated /rooms endpoint (access,
    // accesses, pendencies, settings, unread counters) stay unmapped here -
    // the rated-posts response deliberately does not carry room-level
    // administrative data, and a room reached through a post is a room the
    // reader already reads, so CanView keeps its default of true.
    [MapperIgnoreTarget(nameof(ApiRoom.PreviousRoomId))]
    [MapperIgnoreTarget(nameof(ApiRoom.Access))]
    [MapperIgnoreTarget(nameof(ApiRoom.Type))]
    [MapperIgnoreTarget(nameof(ApiRoom.Accesses))]
    [MapperIgnoreTarget(nameof(ApiRoom.IsArchived))]
    [MapperIgnoreTarget(nameof(ApiRoom.Pendencies))]
    [MapperIgnoreTarget(nameof(ApiRoom.UnreadPostsCount))]
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

    // Create-post dice spec (API -> domain). Bonus and Comment travel by
    // name; the rest are renamed to the domain's XdY vocabulary and the
    // "public" flag is inverted into "hidden".
    private static DomainCreatePostDiceRoll ToCreatePostDiceRoll(CreatePostDiceRoll roll) => new()
    {
        EdgesCount = roll.Dice,
        DiceCount = roll.Count,
        Bonus = roll.Bonus,
        ExplosionCount = roll.Explosion,
        IsHidden = !roll.Public,
        Comment = roll.Comment
    };

    // Only the address is composed here; every other field travels as it is.
    // The domain model carries no object key at all, so there is nothing to
    // remember to leave out.
    private static PostAttachment ToPostAttachment(DomainPostAttachment attachment) => new()
    {
        Id = attachment.Id,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        SizeBytes = attachment.SizeBytes,
        Width = attachment.Width,
        Height = attachment.Height,
        CreatedUtc = attachment.CreatedUtc,
        Url = UploadContentRoute.For(attachment.Id)
    };

    private static PostEditInfo ToPostEditInfo(DomainPostEdit edit) => new()
    {
        Id = edit.Id,
        ModifiedUtc = edit.ModifiedUtc,
        Editor = edit.Editor == null ? null! : UserRefMappers.ToUserRef(edit.Editor)
    };
}
