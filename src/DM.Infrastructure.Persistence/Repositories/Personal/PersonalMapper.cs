using System.Linq;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.ProfileNotes;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;
using DbUserProfileNote = DM.Infrastructure.Persistence.Entities.Account.UserProfileNote;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <summary>
/// Projection formulas for the personal-space DTOs: both flatten a username
/// off a navigation, which is query logic, so they are written out.
/// </summary>
internal static class PersonalMapper
{
    /// <summary>
    /// EF projection to a blacklist entry
    /// </summary>
    public static IQueryable<BlacklistEntry> ProjectToBlacklistEntry(
        this IQueryable<DbUserBlacklist> query) =>
        query.Select(b => new BlacklistEntry
        {
            Id = b.EntryId,
            Username = b.BlockedUser.Username,
            CreatedUtc = b.CreatedUtc
        });

    /// <summary>
    /// Settings row to the domain document. Paging is columns in the row and
    /// an object in the domain: the mapping is where the two shapes meet.
    /// </summary>
    public static UserSettings ToUserSettings(this DbUserSettings settings) => new()
    {
        Id = settings.UserId,
        Theme = settings.Theme,
        Paging = new PagingSettings
        {
            TopicsPerPage = settings.TopicsPerPage,
            CommentsPerPage = settings.CommentsPerPage,
            PostsPerPage = settings.PostsPerPage,
            MessagesPerPage = settings.MessagesPerPage,
            EntitiesPerPage = settings.EntitiesPerPage
        }
    };

    /// <summary>
    /// EF projection to a personal profile note
    /// </summary>
    public static IQueryable<UserProfileNote> ProjectToUserProfileNote(
        this IQueryable<DbUserProfileNote> query) =>
        query.Select(n => new UserProfileNote
        {
            Id = n.UserProfileNoteId,
            SubjectUsername = n.SubjectUser.Username,
            SubjectUserId = n.SubjectUserId,
            Text = n.Text,
            CreatedUtc = n.CreatedUtc,
            ModifiedUtc = n.ModifiedUtc
        });
}
