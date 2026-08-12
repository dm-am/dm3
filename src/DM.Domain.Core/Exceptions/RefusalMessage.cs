using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusal texts that more than one throw site needs
/// </summary>
/// <remarks>
/// The message of an <see cref="HttpException"/> becomes the title of the problem
/// document, and the title is the sentence the client shows the reader. These are
/// therefore product copy in Russian, not developer strings: an English message
/// here is a Russian-speaking user reading English.
///
/// Only texts used from more than one place live here. A refusal thrown once stays
/// at its call site, where a constant would only hide the sentence from the single
/// file that cares about it.
/// </remarks>
public static class RefusalMessage
{
    // ═══ AUTHORIZATION ═══

    /// <summary>
    /// The caller is known and simply may not do this
    /// </summary>
    /// <remarks>
    /// Also the text the middleware answers with for
    /// <see cref="IntentionManagerException"/>: an intention refusal and a
    /// hand-written permission check are the same event to the reader, and two
    /// wordings for it would eventually disagree.
    /// </remarks>
    public const string AccessDenied = "Недостаточно прав для этого действия";

    /// <summary>
    /// The endpoint needs an identity and the caller is anonymous
    /// </summary>
    public const string AuthenticationRequired = "Требуется авторизация";

    // ═══ USERS ═══

    /// <summary>
    /// The user is unknown and the request did not name them
    /// </summary>
    public const string UserNotFound = "Пользователь не найден";

    /// <summary>
    /// The user is unknown and the caller supplied the username, so echoing it
    /// discloses nothing the caller did not already send
    /// </summary>
    public static string UserNotFoundByUsername(string username) =>
        $"Пользователь {username} не найден";

    /// <summary>
    /// The user is unknown and the caller addressed them by id
    /// </summary>
    public static string UserNotFoundById(Guid userId) =>
        $"Пользователь с ID {userId} не найден";

    /// <summary>
    /// The author already wrote a recommendation about this user; one per pair
    /// </summary>
    public const string AlreadyEndorsedUser = "Вы уже рекомендовали этого пользователя";

    /// <summary>
    /// The subscription the request addressed does not exist, or is not the
    /// caller's
    /// </summary>
    public const string SubscriptionNotFound = "Подписка не найдена";

    // ═══ GAMES ═══

    /// <summary>
    /// The game is absent, or hidden from this caller
    /// </summary>
    public const string GameNotFound = "Игра не найдена";

    /// <summary>
    /// The room is absent, or belongs to a game this caller cannot read
    /// </summary>
    public const string RoomNotFound = "Комната не найдена";

    /// <summary>
    /// The per-user access grant on a room is absent
    /// </summary>
    public const string RoomAccessNotFound = "Доступ к комнате не найден";

    /// <summary>
    /// The character is absent, or belongs to a game this caller cannot read
    /// </summary>
    public const string CharacterNotFound = "Персонаж не найден";

    /// <summary>
    /// The game post is absent
    /// </summary>
    public const string PostNotFound = "Пост не найден";

    /// <summary>
    /// The game's owner put this caller on its blacklist
    /// </summary>
    public const string BlacklistedFromGame = "Вы в черном списке этой игры";

    /// <summary>
    /// One review per author per game
    /// </summary>
    public const string AlreadyReviewedGame = "Вы уже оставили рецензию на эту игру";

    /// <summary>
    /// One review per author per post
    /// </summary>
    public const string AlreadyReviewedPost = "Вы уже оценили этот пост";

    // ═══ BLOGS AND FORUM ═══

    /// <summary>
    /// The blog is absent, or hidden from this caller
    /// </summary>
    public const string BlogNotFound = "Блог не найден";

    /// <summary>
    /// The rubric is absent, or belongs to another blog
    /// </summary>
    public const string RubricNotFound = "Рубрика не найдена";

    /// <summary>
    /// The blog's owner put this caller on its blacklist
    /// </summary>
    public const string BlacklistedFromBlog = "Вы в черном списке этого блога";

    /// <summary>
    /// The comment is absent, on any of the four commented surfaces
    /// </summary>
    public static string CommentNotFound(Guid commentId) =>
        $"Комментарий {commentId} не найден";

    // ═══ MESSAGING ═══

    /// <summary>
    /// The chat is absent, or the caller is not in it
    /// </summary>
    /// <remarks>
    /// Covers a game chat room too: to the reader both are a chat, and the room
    /// it is attached to is an implementation detail of where it lives.
    /// </remarks>
    public const string ChatNotFound = "Чат не найден";

    /// <summary>
    /// The message is absent, or lies in a chat the caller cannot read
    /// </summary>
    public const string MessageNotFound = "Сообщение не найдено";

    /// <summary>
    /// The user already joined this global chat event
    /// </summary>
    public const string UserAlreadyParticipant = "Пользователь уже участвует";

    // ═══ INVITATIONS ═══

    /// <summary>
    /// The invitation is absent, or was already resolved
    /// </summary>
    public const string InvitationNotFound = "Приглашение не найдено";

    /// <summary>
    /// The invitation exists but its deadline has passed
    /// </summary>
    public const string InvitationExpired = "Срок приглашения истек";

    /// <summary>
    /// The invitation exists and is addressed to somebody else
    /// </summary>
    public const string InvitationNotForYou = "Приглашение адресовано не вам";

    /// <summary>
    /// The invitee is on the blacklist of the game or blog being invited to
    /// </summary>
    public const string CannotInviteBlacklistedUser =
        "Нельзя пригласить пользователя из черного списка";

    /// <summary>
    /// The invitee is on the inviter's own blacklist
    /// </summary>
    public const string CannotInviteBlockedUser =
        "Нельзя пригласить пользователя из личного черного списка";

    // ═══ NOTEPADS AND NOTES ═══

    /// <summary>
    /// The notepad entry is absent, in a personal, game or blog notepad
    /// </summary>
    public const string NotepadEntryNotFound = "Запись не найдена";

    /// <summary>
    /// The moderator note on a profile is absent
    /// </summary>
    public static string ModerationNoteNotFound(Guid noteId) =>
        $"Заметка {noteId} не найдена";

    // ═══ MODERATION ═══

    /// <summary>
    /// The ticket is absent, or belongs to a queue this moderator does not see
    /// </summary>
    public const string TicketNotFound = "Обращение не найдено";

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
    /// The username change request is absent, on the owner's screen and in the
    /// moderation queue alike
    /// </summary>
    public const string UsernameChangeRequestNotFound = "Заявка на смену имени не найдена";

    // ═══ COMMUNITY CATALOGS ═══

    /// <summary>
    /// The achievement category is absent, or was addressed by an id that never
    /// existed
    /// </summary>
    public const string AchievementCategoryNotFound = "Категория достижений не найдена";

    /// <summary>
    /// The achievement type is absent. The catalog calls a type a tier on screen,
    /// and the text follows the screen
    /// </summary>
    public const string AchievementTypeNotFound = "Тир достижения не найден";

    /// <summary>
    /// The award type is absent
    /// </summary>
    public const string AwardTypeNotFound = "Тип награды не найден";

    /// <summary>
    /// The contest series is absent
    /// </summary>
    public const string ContestSeriesNotFound = "Серия конкурсов не найдена";

    /// <summary>
    /// A catalog entry was given an icon key the sprite does not carry. Reaches a
    /// moderator editing achievements or awards, so it says what to do about it
    /// </summary>
    public static string UnknownIconName(string iconName) =>
        $"Неизвестная иконка \"{iconName}\". Сначала добавьте ее в спрайт game-icons.";

    // ═══ LINKS FROM MAIL AND NOTIFICATIONS ═══

    /// <summary>
    /// The token behind the link was consumed, or never existed
    /// </summary>
    public const string LinkInvalidOrUsed = "Ссылка недействительна или уже использована";

    /// <summary>
    /// The approval token behind the link is unknown or past its deadline
    /// </summary>
    public const string LinkInvalidOrExpired = "Ссылка недействительна или устарела";

    // ═══ STATUS TRANSITIONS ═══

    /// <summary>
    /// The requested status is not reachable from the current one
    /// </summary>
    public const string UnknownStatusTransition = "Недопустимая смена статуса";

    /// <summary>
    /// The move is one of the defined ones, but not from the state the subject
    /// is in now. Said by the module status machine and by the character one,
    /// which are two machines over two vocabularies and one sentence.
    /// </summary>
    public static string IllegalStatusTransition(object transition, object currentStatus) =>
        $"Переход \"{transition}\" недоступен из статуса \"{currentStatus}\"";

    /// <summary>
    /// The same refusal for a module, which also names the reason it is closed
    /// for: Closed is the only status that stores one.
    /// </summary>
    public static string IllegalStatusTransition(
        object transition, ModuleStatus currentStatus, ClosedReason closedReason) =>
        IllegalStatusTransition(transition, currentStatus) +
        (currentStatus == ModuleStatus.Closed ? $" ({closedReason})" : "");

    /// <summary>
    /// The requested premoderation status is not reachable from the current one
    /// </summary>
    public const string UnknownPremoderationTransition = "Недопустимая смена статуса премодерации";

    /// <summary>
    /// Premoderation can only be asked for from some states, and this is not one
    /// </summary>
    public static string CannotSubmitForPremoderation(object currentStatus) =>
        $"Нельзя отправить на премодерацию из статуса \"{currentStatus}\"";

    /// <summary>
    /// Premoderation can only be withdrawn from while it is pending
    /// </summary>
    public static string CannotWithdrawFromPremoderation(object currentStatus) =>
        $"Нельзя снять с премодерации из статуса \"{currentStatus}\"";

    // ═══ FORUM ═══

    /// <summary>
    /// The board is absent. Addressed by alias or by title, and both are echoed
    /// back because the caller sent them
    /// </summary>
    public static string BoardNotFound(string aliasOrTitle) =>
        $"Раздел {aliasOrTitle} не найден";

    // ═══ TAGS ═══

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

    /// <summary>
    /// The viewer already liked this. Raised twice on one path: once from the loaded
    /// collection and once from the unique index, which answers after the competing
    /// request has committed.
    /// </summary>
    public const string AlreadyLiked = "Вы уже поставили лайк";

    // ═══ FIELDS ═══

    /// <summary>
    /// Title of a refusal whose substance is the per-field errors beside it
    /// </summary>
    /// <remarks>
    /// The same sentence the middleware gives a validation failure: to the reader
    /// a rejected form is one event, whether the rules were checked by the
    /// validator or by hand in a controller.
    /// </remarks>
    public const string InvalidData = "Некорректные данные";

    /// <summary>
    /// The password is in a breach corpus. Reaches the reader under the password
    /// field itself, so it says what to do rather than what happened
    /// </summary>
    public const string PasswordBreached =
        "Этот пароль скомпрометирован утечкой, выберите другой";

    /// <summary>Голос в этом опросе уже отдан</summary>
    public const string AlreadyVoted = "Вы уже голосовали в этом опросе";
}
