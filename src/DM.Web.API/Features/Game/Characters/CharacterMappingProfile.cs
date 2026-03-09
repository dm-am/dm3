using AutoMapper;
using DM.Domain.Game.Features.Characters;
using DtoCharacter = DM.Domain.Game.Features.Games.Character;
using DtoCharacterAttribute = DM.Domain.Game.Features.Games.CharacterAttribute;
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
        // Base Character mapping (lightweight, for lists)
        CreateMap<DtoCharacter, Character>();

        // CharacterDetails mapping (full)
        CreateMap<DtoCharacter, CharacterDetails>()
            .ForMember(c => c.Privacy, s => s.MapFrom<AccessPolicyConverter>());

        CreateMap<DtoCharacterAttribute, CharacterAttribute>()
            .ReverseMap();

        // For character creation, use CharacterDetails (has Privacy)
        CreateMap<CharacterDetails, DtoCreateCharacter>()
            .ForMember(c => c.IsNpc, s => s.MapFrom(c => c.Privacy != null && c.Privacy.IsNpc))
            .ForMember(c => c.AccessPolicy, s => s.MapFrom<AccessPolicyConverter>());

        // For character update, use CharacterDetails (has Privacy)
        CreateMap<CharacterDetails, DtoUpdateCharacter>()
            .ForMember(c => c.IsNpc, s => s.MapFrom(c => c.Privacy != null && c.Privacy.IsNpc))
            .ForMember(c => c.AccessPolicy, s => s.MapFrom<AccessPolicyConverter>());
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
