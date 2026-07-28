using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Blacklists;
using DomainBlacklistEntry = DM.Domain.Personal.Features.Blacklists.BlacklistEntry;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// AutoMapper profile for Blacklist mappings
/// </summary>
internal class BlacklistMappingProfile : Profile
{
    /// <inheritdoc />
    public BlacklistMappingProfile()
    {
        CreateMap<DomainBlacklistEntry, BlacklistEntry>();

        // Enum → DTO (for API responses)
        CreateMap<UserBlacklistSettings, BlacklistSettings>()
            .ConvertUsing(src => new BlacklistSettings
            {
                HideComments = src.HasFlag(UserBlacklistSettings.HideComments),
                HideMessages = src.HasFlag(UserBlacklistSettings.HideMessages),
                HideGames = src.HasFlag(UserBlacklistSettings.HideGames),
                HideBlogs = src.HasFlag(UserBlacklistSettings.HideBlogs),
                BlockDirectMessages = src.HasFlag(UserBlacklistSettings.BlockDirectMessages),
                AutoPopulateContentBlacklist = src.HasFlag(UserBlacklistSettings.AutoPopulateContentBlacklist)
            });

        // There is deliberately no DTO → enum map: the write model is
        // UpdateBlacklistSettingsRequest, and folding it onto the current flags
        // needs the current value, which a mapper does not have.
    }
}
