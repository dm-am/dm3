using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// AutoMapper profile for the moderated view of a user profile.
/// </summary>
/// <remarks>
/// <see cref="ModeratedProfile"/> derives from <see cref="UserProfile"/>, and
/// AutoMapper does not follow that inheritance on its own: without this map it
/// resolved <c>Map&lt;ModeratedProfile&gt;(user)</c> to the base configuration,
/// built a <see cref="UserProfile"/> and threw InvalidCastException — the whole
/// moderated profile answered 500. IncludeBase keeps the member mapping in one
/// place; only the derived type is declared here.
/// </remarks>
internal class ModeratedProfileMappingProfile : Profile
{
    /// <inheritdoc />
    public ModeratedProfileMappingProfile()
    {
        CreateMap<GeneralUser, ModeratedProfile>()
            .IncludeBase<GeneralUser, UserProfile>();
    }
}
