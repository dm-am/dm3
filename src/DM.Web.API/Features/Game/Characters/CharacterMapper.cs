using System;
using System.Linq;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using AttributeType = DM.Domain.Core.Enums.AttributeSpecificationType;
using DtoCharacter = DM.Domain.Game.Features.Characters.Character;
using DtoCharacterShort = DM.Domain.Game.Features.Characters.CharacterShort;
using DtoCharacterAttribute = DM.Domain.Game.Features.Characters.CharacterAttribute;
using DtoCreateCharacter = DM.Domain.Game.Features.Characters.CreateCharacter;
using DtoUpdateCharacter = DM.Domain.Game.Features.Characters.UpdateCharacter;
using Policy = DM.Domain.Core.Enums.CharacterAccessPolicy;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// Compile-time mapper for game character models. The AccessPolicy flags fold to
/// and from the privacy block in plain methods; rooms and posts compose this one
/// via <c>[UseMapper]</c>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
public partial class CharacterMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public CharacterMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain character to the list-level character DTO. An NPC has no author,
    /// nullable on both sides.
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
    /// Domain character to the full details DTO, with the owner bound to the
    /// BBCode attribute values.
    /// </summary>
    /// <param name="character">The character as the viewer reads it</param>
    /// <param name="viewerMayEdit">
    /// Whether the viewer may edit it (the leads of an NPC, or of a sheet that
    /// allows it): such a viewer gets its BBCode values as written, as the player does.
    /// </param>
    public CharacterDetails ToCharacterDetails(DtoCharacter character, bool viewerMayEdit = false)
    {
        if (character == null)
        {
            return null!;
        }

        var result = ToCharacterDetailsCore(character);
        // Materialised: a deferred Select would rebuild the attributes the envelope is stamped on.
        result.Attributes = [.. character.Attributes.Select(ToCharacterAttribute)];
        result.AuthorRating = AuthorRatingOf(character.Author);
        result.Privacy = new CharacterPrivacySettings
        {
            IsNpc = character.IsNpc,
            EditByMaster = (character.AccessPolicy & Policy.EditAllowed) != Policy.NoAccess,
            EditPostByMaster = (character.AccessPolicy & Policy.PostEditAllowed) != Policy.NoAccess
        };
        EnvelopeAttributeValues(result, character.Author?.UserId, viewerMayEdit);
        return result;
    }

    /// <summary>
    /// Binds the character's player to every BBCode attribute value, who gets
    /// its AuthorEdit source, and so does a viewer who may edit the character.
    /// </summary>
    public static void EnvelopeAttributeValues(CharacterDetails details, Guid? ownerUserId, bool viewerMayEdit)
    {
        foreach (var attribute in details.Attributes)
        {
            attribute.ValueBbText?.BindAuthor(ownerUserId, viewerMayEdit ? _ => true : null);
        }
    }

    /// <summary>
    /// Create body to the write model. GameId comes from the route, the service sets it.
    /// </summary>
    public static DtoCreateCharacter ToCreateCharacter(CharacterDetails character) => new()
    {
        Name = character.Name,
        IsNpc = character.Privacy != null && character.Privacy.IsNpc,
        AccessPolicy = character.Privacy == null ? Policy.NoAccess : ResolveAccessPolicy(character.Privacy),
        Attributes = [.. character.Attributes.Select(a => ToDtoCharacterAttribute(a.Id, a.Value))]
    };

    /// <summary>
    /// Update request to the write model. CharacterId comes from the route,
    /// the service sets it. An omitted privacy block stays absent: a plain false
    /// would turn an NPC into a player character.
    /// </summary>
    public static DtoUpdateCharacter ToUpdateCharacter(UpdateCharacterRequest request) => new()
    {
        Name = request.Name,
        IsNpc = request.Privacy?.IsNpc,
        AccessPolicy = request.Privacy == null ? null : ResolveAccessPolicy(request.Privacy),
        Attributes = [.. request.Attributes.Select(a => ToDtoCharacterAttribute(a.Id, a.Value))]
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
    // Folded from IsNpc + AccessPolicy in the wrapper.
    [MapperIgnoreTarget(nameof(CharacterDetails.Privacy))]
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

    // Input: only the specification id and the raw value reach the domain (ValueBbText is output-only).
    private static DtoCharacterAttribute ToDtoCharacterAttribute(Guid id, string? value) => new()
    {
        Id = id,
        Value = value!
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

    // Null for an NPC and for an author who switched the rating off, as in UserMapper.
    private static Rating? AuthorRatingOf(GeneralUser? author) =>
        author != null && !author.RatingDisabled ? UserMapper.RatingOf(author) : null;
}
