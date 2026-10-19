using DM.Domain.Core.Users;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusals of bans, tickets, tags, uploads and account restoration
/// </summary>
public static partial class RefusalMessage
{
    // Bans, tickets, uploads and restoration

    /// <summary>The user has no active ban to show</summary>
    public const string UserNotBanned = "Пользователь не забанен";

    /// <summary>
    /// The ticket is absent, or belongs to a queue this moderator does not see
    /// </summary>
    public const string TicketNotFound = "Обращение не найдено";

    /// <summary>
    /// The ticket was refused before it was read: the bot field of the form was filled
    /// </summary>
    public const string TicketNotFiled = "Не удалось отправить обращение";

    /// <summary>
    /// The tag group is absent
    /// </summary>
    public static string TagGroupNotFound(Guid groupId) =>
        $"Группа тегов {groupId} не найдена";

    /// <summary>
    /// The tag is absent
    /// </summary>
    public static string TagNotFound(Guid tagId) =>
        $"Тег {tagId} не найден";

    /// <summary>
    /// The uploaded file is absent, or was deleted
    /// </summary>
    public const string UploadNotFound = "Файл не найден";

    /// <summary>
    /// The removed account is named like <see cref="RemovedAccount.Name" />, so
    /// brought back it would pass for a removed one
    /// </summary>
    public const string ReservedNameNotRestorable =
        $"Аккаунт с именем \"{RemovedAccount.Name}\" восстановить нельзя";

    // Tags

    /// <summary>
    /// A tag group by that title is already in the catalog
    /// </summary>
    public static string TagGroupTitleTaken(string title) =>
        $"Группа тегов \"{title}\" уже есть";

    /// <summary>
    /// A tag by that title is already in the group
    /// </summary>
    public static string TagTitleTaken(string title, string groupTitle) =>
        $"Тег \"{title}\" уже есть в группе \"{groupTitle}\"";
}
