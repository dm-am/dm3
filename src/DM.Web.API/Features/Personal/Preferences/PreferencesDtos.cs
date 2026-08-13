using System;
using DM.Domain.Core.Dto;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// User display preferences
/// </summary>
/// <remarks>
/// Used for: GET/PATCH /v1/users/me/preferences
/// Controls how user sees the website (not part of profile).
/// Returned in LoginResponse for immediate UI application.
/// </remarks>
public class Preferences
{
    /// <summary>
    /// Website color theme
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// Paging settings for various list views
    /// </summary>
    public Paging Paging { get; set; } = new();
}

/// <summary>
/// User paging preferences for various list views
/// </summary>
/// <remarks>
/// Allowed values and the default come from PagingPolicy.
/// </remarks>
public class Paging : IValidatableObject
{
    /// <summary>
    /// Number of posts per page in game rooms
    /// </summary>
    public int PostsPerPage { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// Number of comments per page on games or topics
    /// </summary>
    public int CommentsPerPage { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// Number of topics per page on forum boards
    /// </summary>
    public int TopicsPerPage { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// Number of messages per page in conversations
    /// </summary>
    public int MessagesPerPage { get; set; } = PagingPolicy.DefaultPageSize;

    /// <summary>
    /// Number of items per page for other entity lists (users, games, etc.)
    /// </summary>
    public int EntitiesPerPage { get; set; } = PagingPolicy.DefaultPageSize;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsAllowed(PostsPerPage))
            yield return new ValidationResult("Недопустимое значение постов на страницу", new[] { nameof(PostsPerPage) });

        if (!IsAllowed(CommentsPerPage))
            yield return new ValidationResult("Недопустимое значение комментариев на страницу", new[] { nameof(CommentsPerPage) });

        if (!IsAllowed(TopicsPerPage))
            yield return new ValidationResult("Недопустимое значение тем на страницу", new[] { nameof(TopicsPerPage) });

        if (!IsAllowed(MessagesPerPage))
            yield return new ValidationResult("Недопустимое значение сообщений на страницу", new[] { nameof(MessagesPerPage) });

        if (!IsAllowed(EntitiesPerPage))
            yield return new ValidationResult("Недопустимое значение записей на страницу", new[] { nameof(EntitiesPerPage) });
    }

    private static bool IsAllowed(int value) => PagingPolicy.Allows(value);
}

/// <summary>
/// User profile and display settings
/// </summary>
/// <remarks>
/// These settings control how the user's profile appears to others
/// and their personal display preferences.
/// </remarks>
public class UserSettings
{
    /// <summary>
    /// Whether the user's birthday is visible to other users
    /// </summary>
    public bool IsBirthdayVisible { get; set; }

    /// <summary>
    /// Whether the user's birth year is visible (only applies when birthday is visible)
    /// </summary>
    public bool IsBirthdayYearVisible { get; set; }

    /// <summary>
    /// Website color theme preference
    /// </summary>
    public Theme Theme { get; set; }

    /// <summary>
    /// Paging limits for various list views
    /// </summary>
    public Paging Paging { get; set; } = new();
}
