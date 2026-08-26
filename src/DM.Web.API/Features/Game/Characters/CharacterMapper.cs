using System;
using System.Linq;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using AttributeType = DM.Domain.Core.Enums.AttributeSpecificationType;
using DtoCharacter = DM.Domain.Game.Features.Games.Character;
using DtoCharacterShort = DM.Domain.Game.Features.Games.CharacterShort;
using DtoCharacterAttribute = DM.Domain.Game.Features.Games.CharacterAttribute;
using DtoCreateCharacter = DM.Domain.Game.Features.Characters.CreateCharacter;
using DtoUpdateCharacter = DM.Domain.Game.Features.Characters.UpdateCharacter;
using Policy = DM.Domain.Core.Enums.CharacterAccessPolicy;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// Compile-time mapper for game character models. The Mapperly counterpart of
/// the character profile: the avatar goes through
/// <see cref="UserMapper.ToUserPicture"/> instead of the converter
/// indirection, the AccessPolicy flags fold to and from the privacy block in
/// plain methods, and other mappers (rooms, posts) compose this one via
/// <c>[UseMapper]</c>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class CharacterMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public CharacterMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain character to the list-level character DTO. An NPC has no
    /// author, and both sides of the mapping say so - the domain member is
    /// nullable and so is the DTO one, which is what lets Mapperly carry the
    /// absence through instead of throwing on it.
    /// </summary>
    public Character ToCharacter(DtoCharacter character)
    {
        if (character == null)
        {
            return null!;
        }

        var result = ToCharacterCore(character);
        result.AuthorRating = AuthorRatingOf(character.Author);
        return result;
    }

    /// <summary>
    /// Domain character to the full details DTO. The explicit envelope step
    /// replaces the AutoMapper AfterMap - it must run on every details
    /// response, or the owner's BBCode attribute round-trip silently breaks.
    /// </summary>
    public CharacterDetails ToCharacterDetails(DtoCharacter character)
    {
        if (character == null)
        {
            return null!;
        }

        var result = ToCharacterDetailsCore(character);
        // Materialised on purpose: a deferred Select would rebuild the
        // attribute objects on every enumeration and the envelope below
        // would be stamped onto instances nobody sees again.
        result.Attributes = [.. character.Attributes.Select(ToCharacterAttribute)];
        result.AuthorRating = AuthorRatingOf(character.Author);
        result.Privacy = new CharacterPrivacySettings
        {
            IsNpc = character.IsNpc,
            EditByMaster = (character.AccessPolicy & Policy.EditAllowed) != Policy.NoAccess,
            EditPostByMaster = (character.AccessPolicy & Policy.PostEditAllowed) != Policy.NoAccess
        };
        EnvelopeAttributeValues(result, character.Author?.UserId);
        return result;
    }

    /// <summary>
    /// Populates the render-context envelope on every BBCode attribute value.
    /// Owner of the values is the character's player: the JSON converter then
    /// honors the owner's AuthorEdit round-trip and downgrades any other
    /// viewer's author_edit request to permission-filtered Display.
    /// (getCharacterForEdit sends X-Dm-Audience: author_edit.)
    /// </summary>
    public static void EnvelopeAttributeValues(CharacterDetails details, Guid? ownerUserId)
    {
        foreach (var attribute in details.Attributes)
        {
            if (attribute.ValueBbText is null)
            {
                continue;
            }

            attribute.ValueBbText.Context = new RenderContextEnvelope
            {
                Surface = attribute.ValueBbText.Surface,
                PostAuthorUserId = ownerUserId
            };
        }
    }

    /// <summary>
    /// Create body to the write model. GameId comes from the route and
    /// InitialStatus from the caller's game role - the service sets both.
    /// </summary>
    public DtoCreateCharacter ToCreateCharacter(CharacterDetails character) => new()
    {
        Name = character.Name,
        IsNpc = character.Privacy != null && character.Privacy.IsNpc,
        AccessPolicy = character.Privacy == null ? Policy.NoAccess : ResolveAccessPolicy(character.Privacy),
        Attributes = [.. character.Attributes.Select(ToDtoCharacterAttribute)]
    };

    /// <summary>
    /// Update request to the write model. CharacterId comes from the route,
    /// the service sets it. An omitted privacy block must stay absent: the
    /// destination flags are nullable on purpose, because a plain false here
    /// is the bug that demoted an NPC to a player character on every PATCH
    /// without a privacy block.
    /// </summary>
    public DtoUpdateCharacter ToUpdateCharacter(UpdateCharacterRequest request) => new()
    {
        Name = request.Name!,
        IsNpc = request.Privacy?.IsNpc,
        AccessPolicy = request.Privacy == null ? null : ResolveAccessPolicy(request.Privacy),
        Attributes = [.. request.Attributes.Select(ToDtoCharacterAttribute)]
    };

    /// <summary>
    /// Post-context character projection to the list-level DTO. The domain
    /// CharacterShort carries no retirement flags or roster fields - they
    /// stay default; the picture is filled in batch by
    /// PostRepository.EnrichWithCharacterPictures.
    /// </summary>
    public Character ToCharacter(DtoCharacterShort character)
    {
        if (character == null)
        {
            return null!;
        }

        return ToCharacterShortCore(character);
    }

    // A narrowing projection: the full domain character also carries
    // attributes and game/audit fields the list-level DTO does not answer.
    [MapperIgnoreTarget(nameof(Character.AuthorRating))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Character ToCharacterCore(DtoCharacter character);

    [MapperIgnoreTarget(nameof(Character.TotalPostsCount))]
    [MapperIgnoreTarget(nameof(Character.IsDead))]
    [MapperIgnoreTarget(nameof(Character.IsPlayerLeft))]
    [MapperIgnoreTarget(nameof(Character.IsPlayerExiled))]
    [MapperIgnoreTarget(nameof(Character.LastPostUtc))]
    [MapperIgnoreTarget(nameof(Character.Descriptor))]
    [MapperIgnoreTarget(nameof(Character.AuthorRating))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Character ToCharacterShortCore(DtoCharacterShort character);

    [MapperIgnoreTarget(nameof(Character.AuthorRating))]
    // Folded from IsNpc + AccessPolicy in the wrapper - the flags are one
    // source member, not two, and Mapperly maps a member exactly once.
    [MapperIgnoreTarget(nameof(CharacterDetails.Privacy))]
    // Materialised in the wrapper so the envelope survives enumeration.
    [MapperIgnoreTarget(nameof(CharacterDetails.Attributes))]
    [MapperIgnoreSource(nameof(DtoCharacter.Attributes))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial CharacterDetails ToCharacterDetailsCore(DtoCharacter character);

    // Output: BbCode-typed values become a server-rendered InfoBbText
    // payload; all other types carry the plain string. The raw stored BBCode
    // is never placed into the plain Value (HTML sink).
    private static CharacterAttribute ToCharacterAttribute(DtoCharacterAttribute attribute) => new()
    {
        Id = attribute.Id,
        Title = attribute.Title,
        Value = attribute.Type == AttributeType.BbCode ? null : attribute.Value,
        ValueBbText = attribute.Type == AttributeType.BbCode ? new InfoBbText { Value = attribute.Value } : null,
        Modifier = attribute.Modifier,
        Inconsistent = attribute.Inconsistent
    };

    // Input: the client always submits the raw value in Value (ValueBbText is
    // output-only). Only the specification id and the raw value reach the
    // domain update path.
    private static DtoCharacterAttribute ToDtoCharacterAttribute(CharacterAttribute attribute) => new()
    {
        Id = attribute.Id,
        Value = attribute.Value!
    };

    private static DtoCharacterAttribute ToDtoCharacterAttribute(UpdateCharacterAttribute attribute) => new()
    {
        Id = attribute.Id,
        Value = attribute.Value!
    };

    private static Policy ResolveAccessPolicy(CharacterPrivacySettings privacy)
    {
        var result = Policy.NoAccess;
        if (privacy.EditByMaster)
        {
            result |= Policy.EditAllowed;
        }

        if (privacy.EditPostByMaster)
        {
            result |= Policy.PostEditAllowed;
        }

        return result;
    }

    // Author rating for the "Рейтинг" roster column. Null for NPCs (no
    // author) and for authors who disabled rating display - same rule as
    // the GeneralUser -> User mapping in UserMapper.
    private static Rating? AuthorRatingOf(GeneralUser? author) =>
        author != null && !author.RatingDisabled
            ? new Rating { TotalPosts = author.QuantityRating, PostReviewScoreSum = author.QualityRating }
            : null;
}
