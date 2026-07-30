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
            .IncludeBase<GeneralUser, UserProfile>()
            // Filled by ModeratedProfileApiService after the map: each comes from
            // a repository of its own, and half of them depend on the caller's
            // role rather than on the user being read. Declared rather than left
            // silent so the configuration assertion keeps covering the members
            // that ARE this map's job.
            .ForMember(d => d.IpAddresses, o => o.Ignore())
            .ForMember(d => d.LoginHistory, o => o.Ignore())
            .ForMember(d => d.LinkedProfiles, o => o.Ignore())
            .ForMember(d => d.ModeratorNotes, o => o.Ignore())
            .ForMember(d => d.Violations, o => o.Ignore())
            .ForMember(d => d.Permissions, o => o.Ignore());
    }
}
