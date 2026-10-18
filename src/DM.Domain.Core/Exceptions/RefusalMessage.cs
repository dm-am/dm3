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

    /// <summary>
    /// Too many wrong passwords in a row closed this door for a while
    /// </summary>
    /// <remarks>
    /// The screen spells the wait itself, out of the seconds that travel beside
    /// this title, and in the sentence it already uses for a rate limit. The
    /// title stays the name of what happened, for a caller reading the document
    /// without a screen.
    /// </remarks>
    public const string LoginLocked = "Вход заблокирован";

    /// <summary>
    /// The credentials were proven and moderation has closed the account
    /// </summary>
    /// <remarks>
    /// "Забанен", not "заблокирован": the security history captions the lockout
    /// after failed attempts "Аккаунт заблокирован", and that is a different
    /// event. One word, one meaning. Said by both halves of a login, because a
    /// ban issued between the two of them refuses at the second one.
    /// </remarks>
    public const string AccountBanned = "Аккаунт забанен";

    /// <summary>
    /// The credentials were proven and the account no longer exists
    /// </summary>
    public const string AccountRemoved = "Аккаунт удален";

    // ═══ SECOND FACTOR ═══

    /// <summary>
    /// The current password was asked for again and did not match
    /// </summary>
    /// <remarks>
    /// Said wherever a change of the account's own credentials is confirmed by
    /// retyping the password: deactivation, and every operation on the second
    /// factor. One sentence, because to the reader it is one event.
    /// </remarks>
    public const string WrongPassword = "Неверный пароль";

    /// <summary>
    /// The single answer to every way of failing the second factor
    /// </summary>
    /// <remarks>
    /// A wrong code, an expired challenge, a challenge nobody issued, a recovery
    /// code already spent and a challenge out of attempts all come back as this
    /// sentence and nothing else. Anything narrower tells whoever is guessing
    /// which of the five walls they are standing at, and how many tries are left
    /// - which the password path deliberately does not say either.
    /// </remarks>
    public const string TwoFactorRejected = "Неверный код";

    /// <summary>
    /// An operation that needs the factor was addressed to an account without one
    /// </summary>
    public const string TwoFactorNotEnabled = "Двухфакторная аутентификация не включена";

    /// <summary>
    /// Switching on what is already on; replacing a device is off and on again
    /// </summary>
    public const string TwoFactorAlreadyEnabled = "Двухфакторная аутентификация уже включена";

    /// <summary>
    /// The issued secret was never confirmed inside the window and is gone
    /// </summary>
    public const string TwoFactorSetupExpired =
        "Настройка двухфакторной аутентификации устарела, начните заново";


    // ═══ USERS ═══

    /// <summary>
    /// The user is unknown and the request did not name them
    /// </summary>
    public const string UserNotFound = "Пользователь не найден";

    // ═══ BLACKLISTS OF A GAME AND OF A BLOG ═══
    //
    // One wording for both lists. The two services managing them used to answer
    // the same fact in two sentences ("...уже в черном списке" with the name and
    // without it, "Сначала удалите..." against "Сначала уберите..."), from string
    // literals of their own. The name is not repeated: the owner has just typed
    // it into the field the answer appears under.

    /// <summary>
    /// The same person is already on the game's or the blog's blacklist
    /// </summary>
    /// <remarks>
    /// Thrown by the pre-check of the service and by the translation of the
    /// unique index the game repository hits when two requests race past that
    /// pre-check: one sentence for one fact, whichever path found it.
    /// </remarks>
    public const string AlreadyInBlacklist = "Пользователь уже в черном списке";

    /// <summary>
    /// The person asked to be taken off a blacklist is not on it
    /// </summary>
    public const string NotInBlacklist = "Пользователя нет в черном списке";

    /// <summary>
    /// The master and the curating mentor run the game and cannot be shut out of it
    /// </summary>
    public const string CannotBlacklistGameLead =
        "Мастера и наставника игры нельзя внести в черный список";

    /// <summary>
    /// The author and the curating mentor run the blog and cannot be shut out of it
    /// </summary>
    public const string CannotBlacklistBlogLead =
        "Автора и наставника блога нельзя внести в черный список";

    /// <summary>
    /// An assistant or a player is part of the game and has to leave it first
    /// </summary>
    public const string RemoveFromGameFirst = "Сначала уберите пользователя из игры";

    /// <summary>
    /// An assistant is part of the blog and has to leave it first
    /// </summary>
    public const string RemoveFromBlogAssistantsFirst =
        "Сначала уберите пользователя из ассистентов блога";

    /// <summary>
    /// A member already has a website testimonial written on their behalf
    /// </summary>
    /// <remarks>
    /// Same pairing: the service pre-check and the repository's translation of
    /// the partial unique index by author answer with one sentence.
    /// </remarks>
    public static string TestimonialAlreadyExists(string username) =>
        $"У участника {username} уже есть отзыв";

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
    /// <remarks>
    /// Said by GameRoleExtensions.ThrowIfBlacklisted and by nothing else: both
    /// actions the list closes, the discussion and the application, ask that one
    /// method, so the answer reads the same under either.
    /// </remarks>
    public const string BlacklistedFromGame = "Вы в черном списке этой игры";

    /// <summary>
    /// One review per author per game
    /// </summary>
    public const string AlreadyReviewedGame = "Вы уже оставили рецензию на эту игру";

    /// <summary>
    /// One review per author per post
    /// </summary>
    public const string AlreadyReviewedPost = "Вы уже оценили этот пост";

    /// <summary>
    /// Probation: below the post threshold a rating may only be neutral. Said
    /// both when the review is written and when an edit tries to move its sign,
    /// which is the same rule reached by two doors
    /// </summary>
    public static string SignedReviewNeedsExperience(int postThreshold) =>
        $"Ставить плюс и минус можно после {postThreshold} постов в играх";

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
    /// <remarks>
    /// Said by BlogRoleExtensions.ThrowIfBlacklisted and by nothing else, the
    /// blog side of BlacklistedFromGame.
    /// </remarks>
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
    /// The reader signed himself up for a global chat event he is already on
    /// </summary>
    /// <remarks>
    /// Second person, because the reader pressed the button about himself and
    /// the answer is about him. The other door to the same list is opened by an
    /// organizer about somebody else and answers with
    /// <see cref="UserAlreadyParticipant" />.
    ///
    /// Said by the read that looks before the join and by the write that finds
    /// the pair already there: two clicks racing past that read are the same
    /// fact to the reader as a second click after it.
    /// </remarks>
    public const string AlreadyParticipant = "Вы уже участвуете в эвенте";

    /// <summary>
    /// An organizer added to a global chat event somebody already on its list
    /// </summary>
    /// <remarks>
    /// Third person: the sentence names the user the request carried, not the
    /// organizer reading it. Same pairing of a read and a write as
    /// <see cref="AlreadyParticipant" />.
    /// </remarks>
    public const string UserAlreadyParticipant = "Пользователь уже участвует в эвенте";

    /// <summary>
    /// Only one event runs at a time, and one is running
    /// </summary>
    /// <remarks>
    /// Same pairing as <see cref="AlreadyParticipant" />: the check before
    /// the start and the partial unique index that catches the two starts which
    /// raced past it answer with one sentence.
    /// </remarks>
    public const string AnotherEventIsLive = "Уже идет другой эвент";

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
    /// The invitee keeps the inviter on their personal blacklist
    /// </summary>
    /// <remarks>
    /// An invitation lands in the invitee's account settings signed with the
    /// inviter's name, so without this refusal it was a way to reach somebody
    /// who had blocked you. The sentence names nobody and gives no reason, the
    /// way a group chat refuses the same person (ChatService): whose list keeps
    /// whom is the list owner's to know.
    ///
    /// The other direction is no longer refused. The inviter's own list restricts
    /// the people on it, not the inviter, and inviting somebody you blocked is a
    /// decision of the only person that list belongs to.
    /// </remarks>
    public const string CannotInviteThisUser = "Нельзя пригласить этого пользователя";

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
    /// There is nothing to restore: the account a moderator aimed at is alive
    /// </summary>
    public const string AccountNotRemoved = "Аккаунт не удален";

    /// <summary>
    /// The removed account is named "Аккаунт удален" or a spelling a reader would
    /// take for it. Brought back, it would be a live account signing its posts the
    /// way everything removed accounts wrote is signed, and holding the address of
    /// the profile that gathers it
    /// </summary>
    public const string ReservedNameNotRestorable =
        "Аккаунт с именем \"Аккаунт удален\" восстановить нельзя";

    /// <summary>
    /// The address and the password do not go together. One sentence for both
    /// halves, so a caller cannot tell which of them was wrong
    /// </summary>
    public const string WrongCredentials = "Неверная почта или пароль";

    /// <summary>
    /// The username change request is absent, on the owner's screen and in the
    /// moderation queue alike
    /// </summary>
    public const string UsernameChangeRequestNotFound = "Заявка на смену имени не найдена";

    /// <summary>
    /// A change is already in flight: the request awaits a moderator. Said by the
    /// check that reads before the write and by the index that catches the two
    /// requests which raced past it
    /// </summary>
    public const string UsernameChangeAlreadyFiled = "Заявка на смену имени уже отправлена";

    /// <summary>
    /// A change is already granted and unspent: the name is chosen from the letter,
    /// not from the settings page
    /// </summary>
    public const string UsernameChangeAlreadyApproved =
        "Заявка уже одобрена: выберите новое имя по ссылке из письма";

    /// <summary>
    /// The chosen name is taken. Said by the read that looks before the rename and
    /// by the translation of the two unique structures the write hits when a name
    /// is claimed between them
    /// </summary>
    public const string UsernameTaken = "Это имя уже занято";

    /// <summary>
    /// The name a rollback aims at is no longer free. Same pairing as
    /// <see cref="UsernameTaken" />, and it names the name because a rollback is
    /// asked for by a moderator who did not choose it
    /// </summary>
    public static string UsernameRollbackBlocked(string username) =>
        $"Откат невозможен: имя {username} уже занято";

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
    /// The starting character sheet is absent, or was hidden
    /// </summary>
    /// <remarks>
    /// Said by the read and by all three writes: a template hidden between the
    /// moment a moderator opened the list and the moment they pressed anything
    /// is the same fact whichever of the four found it.
    /// </remarks>
    public const string AttributeSchemaTemplateNotFound = "Шаблон схемы не найден";

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
    /// The author may ask for a verdict only while the module is awaiting edits.
    /// The two moderation verdicts are legal from every status and never say this.
    /// </summary>
    public static string CannotSubmitForApproval(object currentStatus) =>
        $"Нельзя отправить на подтверждение из статуса \"{currentStatus}\"";

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

    /// <summary>The vote in this poll has already been cast</summary>
    public const string AlreadyVoted = "Вы уже голосовали в этом опросе";
}
