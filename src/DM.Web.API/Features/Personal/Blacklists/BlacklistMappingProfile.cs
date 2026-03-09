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

        // DTO → Enum (for API requests)
        CreateMap<BlacklistSettings, UserBlacklistSettings>()
            .ConvertUsing(src =>
                (src.HideComments ? UserBlacklistSettings.HideComments : UserBlacklistSettings.None) |
                (src.HideMessages ? UserBlacklistSettings.HideMessages : UserBlacklistSettings.None) |
                (src.HideGames ? UserBlacklistSettings.HideGames : UserBlacklistSettings.None) |
                (src.HideBlogs ? UserBlacklistSettings.HideBlogs : UserBlacklistSettings.None) |
                (src.BlockDirectMessages ? UserBlacklistSettings.BlockDirectMessages : UserBlacklistSettings.None) |
                (src.AutoPopulateContentBlacklist ? UserBlacklistSettings.AutoPopulateContentBlacklist : UserBlacklistSettings.None));
    }
}
