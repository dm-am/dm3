using DM.Domain.Core.Dto;
using Riok.Mapperly.Abstractions;
using ServiceUserSettings = DM.Domain.Core.Identity.UserSettings;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// Compile-time mapper between the preferences DTOs and the domain settings
/// document.
/// </summary>
[Mapper]
public partial class PreferencesMapper
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

    // A zeroed page size reads as "no preference". Mapperly applies it to every
    // int member it maps here, in both directions.
    private static int PositiveOrDefault(int value) => value > 0 ? value : PagingPolicy.DefaultPageSize;
}
