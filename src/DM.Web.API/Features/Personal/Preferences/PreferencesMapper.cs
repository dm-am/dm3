using Riok.Mapperly.Abstractions;
using ServicePagingSettings = DM.Domain.Core.Identity.PagingSettings;
using ServiceUserSettings = DM.Domain.Core.Identity.UserSettings;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// Compile-time mapper between the preferences DTOs and the domain settings
/// document.
/// </summary>
[Mapper]
internal partial class PreferencesMapper
{
    /// <summary>
    /// Domain settings document to the preferences DTO
    /// </summary>
    [MapperIgnoreSource(nameof(ServiceUserSettings.Id))]
    public partial Preferences ToPreferences(ServiceUserSettings settings);

    /// <summary>
    /// Preferences DTO to the domain settings document. Id belongs to the
    /// persistence layer and is assigned on write, not carried by the DTO.
    /// </summary>
    [MapperIgnoreTarget(nameof(ServiceUserSettings.Id))]
    public partial ServiceUserSettings ToUserSettings(Preferences preferences);

    private partial ServicePagingSettings ToPagingSettings(Paging paging);

    // A settings document may predate paging or carry zeroed values; both read
    // as "no preference". The fallback of 10 is inherited verbatim from the
    // AutoMapper profile this replaces.
    private static Paging ToPaging(ServicePagingSettings? paging) =>
        paging == null
            ? new Paging()
            : new Paging
            {
                PostsPerPage = PositiveOrDefault(paging.PostsPerPage),
                CommentsPerPage = PositiveOrDefault(paging.CommentsPerPage),
                TopicsPerPage = PositiveOrDefault(paging.TopicsPerPage),
                MessagesPerPage = PositiveOrDefault(paging.MessagesPerPage),
                EntitiesPerPage = PositiveOrDefault(paging.EntitiesPerPage),
            };

    private static int PositiveOrDefault(int value) => value > 0 ? value : 10;
}
