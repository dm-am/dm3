using AutoMapper;
using DM.Domain.Game.Features.Characters;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using AttributeType = DM.Domain.Core.Enums.AttributeSpecificationType;
using DtoCharacter = DM.Domain.Game.Features.Games.Character;
using DtoCharacterAttribute = DM.Domain.Game.Features.Games.CharacterAttribute;
using DtoCharacterShort = DM.Domain.Game.Features.Games.CharacterShort;
using DtoCreateCharacter = DM.Domain.Game.Features.Characters.CreateCharacter;
using DtoUpdateCharacter = DM.Domain.Game.Features.Characters.UpdateCharacter;
using Policy = DM.Domain.Core.Enums.CharacterAccessPolicy;

namespace DM.Web.API.Features.Game.Characters;

/// <inheritdoc />
internal class CharacterMappingProfile : Profile
{
    /// <inheritdoc />
    public CharacterMappingProfile()
    {
        // Base Character mapping (lightweight, for lists).
        // Picture — via the AvatarPicture→UserPicture converter
        // (imgproxy thumbnails on the fly, see AvatarPictureConverter).
        CreateMap<DtoCharacter, Character>()
            .ForMember(d => d.Picture, o => o.MapFrom(s => s.Picture))
            // Author rating for the "Рейтинг" roster column. Null for NPCs (no
            // author) and for authors who disabled rating display - same rule as
            // the GeneralUser -> User mapping in UserMappingProfile.
            .ForMember(d => d.AuthorRating, o => o.MapFrom(s =>
                s.Author != null && !s.Author.RatingDisabled
                    ? new Rating { TotalPosts = s.Author.QuantityRating, PostReviewScoreSum = s.Author.QualityRating }
                    : null));
            // Descriptor and LastPostUtc map by name/convention.

        // CharacterShort -> Character (for Post.Character).
        // Picture is filled in batch by PostRepository.EnrichWithCharacterPictures.
        CreateMap<DtoCharacterShort, Character>()
            .ForMember(d => d.TotalPostsCount, opt => opt.Ignore())
            // Domain CharacterShort is the lightweight post-context projection
            // and carries no retirement flags (dead/left/exiled) — those live
            // only on the full Character. Ignore them here; they stay default.
            .ForMember(d => d.IsDead, opt => opt.Ignore())
            .ForMember(d => d.IsPlayerLeft, opt => opt.Ignore())
            .ForMember(d => d.IsPlayerExiled, opt => opt.Ignore())
            // Roster-only fields; the post-context projection carries none of them.
            .ForMember(d => d.LastPostUtc, opt => opt.Ignore())
            .ForMember(d => d.Descriptor, opt => opt.Ignore())
            .ForMember(d => d.AuthorRating, opt => opt.Ignore())
            .ForMember(d => d.Picture, o => o.MapFrom(s => s.Picture));

        // CharacterDetails mapping (full). Inherits the base Character member
        // config (Picture converter, AuthorRating) via IncludeBase.
        CreateMap<DtoCharacter, CharacterDetails>()
            .IncludeBase<DtoCharacter, Character>()
            .ForMember(c => c.Privacy, s => s.MapFrom<AccessPolicyConverter>())
            .AfterMap((src, dest) =>
            {
                // Owner of the character's BBCode attribute values is the
                // character's player. Populate the render-context envelope on
                // every BBCode attribute so the JSON converter honors the
                // owner's AuthorEdit round-trip and downgrades any other
                // viewer's author_edit request to permission-filtered Display.
                // (getCharacterForEdit sends X-Dm-Audience: author_edit.)
                var ownerUserId = src.Author?.UserId;
                foreach (var attribute in dest.Attributes)
                {
                    if (attribute.ValueBbText is null) continue;
                    attribute.ValueBbText.Context = new RenderContextEnvelope
                    {
                        Surface = attribute.ValueBbText.Surface,
                        PostAuthorUserId = ownerUserId
                    };
                }
            });

        // Output: BbCode-typed values become a server-rendered InfoBbText
        // payload; all other types carry the plain string. The raw stored BBCode
        // is never placed into the plain Value (HTML sink).
        CreateMap<DtoCharacterAttribute, CharacterAttribute>()
            .ForMember(d => d.Value, o => o.MapFrom(s =>
                s.Type == AttributeType.BbCode ? null : s.Value))
            .ForMember(d => d.ValueBbText, o => o.MapFrom(s =>
                s.Type == AttributeType.BbCode ? new InfoBbText { Value = s.Value } : null));

        // Input: the client always submits the raw value in Value (ValueBbText is
        // output-only). Only Id + Value reach the domain update path.
        CreateMap<CharacterAttribute, DtoCharacterAttribute>()
            .ForMember(d => d.AttributeId, o => o.Ignore())
            .ForMember(d => d.Description, o => o.Ignore())
            .ForMember(d => d.Type, o => o.Ignore());

        // For character creation, use CharacterDetails (has Privacy)
        CreateMap<CharacterDetails, DtoCreateCharacter>()
            .ForMember(c => c.IsNpc, s => s.MapFrom(c => c.Privacy != null && c.Privacy.IsNpc))
            .ForMember(c => c.AccessPolicy, s => s.MapFrom<AccessPolicyConverter>())
            .ForMember(c => c.GameId, opt => opt.Ignore())
            .ForMember(c => c.InitialStatus, opt => opt.Ignore());

        // For character update, use CharacterDetails (has Privacy)
        CreateMap<CharacterDetails, DtoUpdateCharacter>()
            // Nullable on purpose: the destination is bool? and "no privacy block
            // in the request" has to stay absent. The `!= null &&` form returns a
            // plain false, which the update path then wrote — patching an NPC
            // without a privacy block demoted it to a player character.
            .ForMember(c => c.IsNpc, s => s.MapFrom(c => c.Privacy != null ? (bool?)c.Privacy.IsNpc : null))
            .ForMember(c => c.AccessPolicy, s => s.MapFrom<AccessPolicyConverter>())
            .ForMember(c => c.CharacterId, opt => opt.Ignore())
            .ForMember(c => c.IsDead, opt => opt.Ignore())
            .ForMember(c => c.IsPlayerLeft, opt => opt.Ignore())
            .ForMember(c => c.IsPlayerExiled, opt => opt.Ignore());
    }

    private class AccessPolicyConverter :
        IValueResolver<CharacterDetails, DtoCreateCharacter, Policy>,
        IValueResolver<CharacterDetails, DtoUpdateCharacter, Policy?>,
        IValueResolver<DtoCharacter, CharacterDetails, CharacterPrivacySettings>
    {
        private static Policy Resolve(CharacterPrivacySettings privacySettings)
        {
            if (privacySettings == null)
            {
                return Policy.NoAccess;
            }

            var result = Policy.NoAccess;
            if (privacySettings.EditByMaster)
            {
                result |= Policy.EditAllowed;
            }

            if (privacySettings.EditPostByMaster)
            {
                result |= Policy.PostEditAllowed;
            }

            return result;
        }

        public Policy Resolve(CharacterDetails source, DtoCreateCharacter destination, Policy destMember,
            ResolutionContext context) =>
            Resolve(source.Privacy);

        public Policy? Resolve(CharacterDetails source, DtoUpdateCharacter destination, Policy? destMember,
            ResolutionContext context) =>
            source.Privacy == null
                ? null
                : Resolve(source.Privacy);

        public CharacterPrivacySettings Resolve(DtoCharacter source,
            CharacterDetails destination, CharacterPrivacySettings destMember,
            ResolutionContext context) => new()
        {
            IsNpc = source.IsNpc,
            EditByMaster = (source.AccessPolicy & Policy.EditAllowed) != Policy.NoAccess,
            EditPostByMaster = (source.AccessPolicy & Policy.PostEditAllowed) != Policy.NoAccess
        };
    }
}
