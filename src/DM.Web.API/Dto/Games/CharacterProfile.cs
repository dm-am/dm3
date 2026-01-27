using AutoMapper;
using DM.Services.Gaming.Dto.Input;
using DtoCharacter = DM.Services.Gaming.Dto.Output.Character;
using Policy = DM.Services.Core.Dto.Enums.CharacterAccessPolicy;

namespace DM.Web.API.Dto.Games;

/// <inheritdoc />
internal class CharacterProfile : Profile
{
    /// <inheritdoc />
    public CharacterProfile()
    {
        // Base Character mapping (lightweight, for lists)
        CreateMap<DtoCharacter, Character>();

        // CharacterDetails mapping (full)
        CreateMap<DtoCharacter, CharacterDetails>()
            .ForMember(c => c.Privacy, s => s.MapFrom<AccessPolicyConverter>());

        CreateMap<DM.Services.Gaming.Dto.Output.CharacterAttribute, CharacterAttribute>()
            .ReverseMap();

        // For character creation, use CharacterDetails (has Privacy)
        CreateMap<CharacterDetails, CreateCharacter>()
            .ForMember(c => c.IsNpc, s => s.MapFrom(c => c.Privacy != null && c.Privacy.IsNpc))
            .ForMember(c => c.AccessPolicy, s => s.MapFrom<AccessPolicyConverter>());

        // For character update, use CharacterDetails (has Privacy)
        CreateMap<CharacterDetails, UpdateCharacter>()
            .ForMember(c => c.IsNpc, s => s.MapFrom(c => c.Privacy != null && c.Privacy.IsNpc))
            .ForMember(c => c.AccessPolicy, s => s.MapFrom<AccessPolicyConverter>());
    }

    private class AccessPolicyConverter :
        IValueResolver<CharacterDetails, CreateCharacter, Policy>,
        IValueResolver<CharacterDetails, UpdateCharacter, Policy?>,
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

        public Policy Resolve(CharacterDetails source, CreateCharacter destination, Policy destMember,
            ResolutionContext context) =>
            Resolve(source.Privacy);

        public Policy? Resolve(CharacterDetails source, UpdateCharacter destination, Policy? destMember,
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
