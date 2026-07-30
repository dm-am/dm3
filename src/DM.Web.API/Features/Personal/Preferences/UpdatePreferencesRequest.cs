using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// Partial update of display preferences
/// </summary>
/// <remarks>
/// Every field is optional: an omitted one keeps its current value. The read
/// model cannot be reused here — its <c>Theme</c> is a plain enum and its
/// <c>Paging</c> default-constructs with every page size at 10, so a request
/// carrying only the theme reset all five page sizes.
/// </remarks>
public class UpdatePreferencesRequest
{
    /// <summary>
    /// Website color theme
    /// </summary>
    public Theme? Theme { get; set; }

    /// <summary>
    /// Paging settings for various list views
    /// </summary>
    public UpdatePagingRequest? Paging { get; set; }
}

/// <summary>
/// Partial update of paging preferences
/// </summary>
/// <remarks>
/// Allowed values: 5, 10, 20, 30, 40, 50, 100, 200. An omitted field keeps its
/// current value.
/// </remarks>
public class UpdatePagingRequest : IValidatableObject
{
    private static readonly int[] AllowedValues = [5, 10, 20, 30, 40, 50, 100, 200];

    /// <summary>
    /// Number of posts per page in game rooms
    /// </summary>
    public int? PostsPerPage { get; set; }

    /// <summary>
    /// Number of comments per page on games or topics
    /// </summary>
    public int? CommentsPerPage { get; set; }

    /// <summary>
    /// Number of topics per page on forum boards
    /// </summary>
    public int? TopicsPerPage { get; set; }

    /// <summary>
    /// Number of messages per page in conversations
    /// </summary>
    public int? MessagesPerPage { get; set; }

    /// <summary>
    /// Number of items per page for other entity lists (users, games, etc.)
    /// </summary>
    public int? EntitiesPerPage { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (value, field) in new[]
                 {
                     (PostsPerPage, nameof(PostsPerPage)),
                     (CommentsPerPage, nameof(CommentsPerPage)),
                     (TopicsPerPage, nameof(TopicsPerPage)),
                     (MessagesPerPage, nameof(MessagesPerPage)),
                     (EntitiesPerPage, nameof(EntitiesPerPage)),
                 })
        {
            if (value.HasValue && !AllowedValues.Contains(value.Value))
            {
                yield return new ValidationResult(
                    "Недопустимое количество элементов на страницу", [field]);
            }
        }
    }
}
