using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// Partial update of display preferences
/// </summary>
/// <remarks>
/// Every field is optional: an omitted one keeps its current value, which the
/// plain values of the read model cannot say.
/// </remarks>
public class UpdatePreferencesRequest : IValidatableObject
{
    /// <summary>
    /// Website color theme
    /// </summary>
    public Theme? Theme { get; set; }

    /// <summary>
    /// Page size of every paged list, one of PagingPolicy.AllowedPageSizes
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// IANA name of the reader's time zone ("Europe/Moscow"), used by letters.
    /// </summary>
    /// <remarks>
    /// Omitted keeps the zone the account has; an empty string clears it to UTC.
    /// </remarks>
    [StringLength(64, ErrorMessage = "Название часового пояса не должно превышать 64 символа")]
    public string? TimeZoneId { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PageSize is { } pageSize && !PagingPolicy.Allows(pageSize))
        {
            yield return new ValidationResult("Недопустимое число на странице", [nameof(PageSize)]);
        }
    }
}
