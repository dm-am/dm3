namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusals of the lists people keep about each other, invitations, notes, likes, polls and the community catalogs
/// </summary>
public static partial class RefusalMessage
{
    // The name is not repeated: the owner has just typed it into the field the answer appears
    // under.

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

    // Testimonials, recommendations and subscriptions

    /// <summary>
    /// A member already has a website testimonial written on their behalf
    /// </summary>
    /// <remarks>
    /// Same pairing: the service pre-check and the repository's translation of
    /// the partial unique index by author answer with one sentence.
    /// </remarks>
    public static string TestimonialAlreadyExists(string username) =>
        $"У пользователя {username} уже есть отзыв";

    /// <summary>
    /// The author already wrote a recommendation about this user; one per pair
    /// </summary>
    public const string AlreadyEndorsedUser = "Вы уже рекомендовали этого пользователя";

    /// <summary>
    /// The subscription the request addressed does not exist, or is not the
    /// caller's
    /// </summary>
    public const string SubscriptionNotFound = "Подписка не найдена";

    /// <summary>The caller holds no subscription to the target the check named</summary>
    public const string NotSubscribed = "Вы не подписаны";

    // Invitations

    /// <summary>
    /// The invitation is absent, or was already resolved
    /// </summary>
    public const string InvitationNotFound = "Приглашение не найдено";

    /// <summary>The invitation the account page settles is absent, or was accepted or declined already</summary>
    public const string InvitationNotFoundOrSettled = "Приглашение не найдено или уже обработано";

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
    /// Names nobody and gives no reason: whose list keeps whom is the list owner's
    /// to know. The inviter's own list does not refuse.
    /// </remarks>
    public const string CannotInviteThisUser = "Нельзя пригласить этого пользователя";

    // Notepads and notes

    /// <summary>
    /// The notepad entry is absent, in a personal, game or blog notepad
    /// </summary>
    public const string NotepadEntryNotFound = "Запись не найдена";

    /// <summary>
    /// The moderator note on a profile is absent
    /// </summary>
    public static string ModerationNoteNotFound(Guid noteId) =>
        $"Заметка {noteId} не найдена";

    /// <summary>The caller keeps no note about this user</summary>
    public const string ProfileNoteNotFound = "Заметка не найдена";

    // Community catalogs

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
    /// The award is absent, or is not held by the user the route names
    /// </summary>
    public const string AwardNotFound = "Награда не найдена";

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

    // Likes ("Нравится") and polls

    /// <summary>
    /// The viewer already liked this. Raised twice on one path: once from the loaded
    /// collection and once from the unique index, which answers after the competing
    /// request has committed.
    /// </summary>
    public const string AlreadyLiked = "Вы уже отметили Нравится";

    /// <summary>The viewer withdraws a like they never left.</summary>
    public const string NotLiked = "Вы еще не отмечали Нравится";

    // The period of the leaderboards

    /// <summary>Title of a refused period; the fields say what is wrong with it</summary>
    public const string PeriodInvalid = "Некорректный период";

    /// <summary>The year is neither the all-time 0 nor within the calendar the boards count</summary>
    public static string PeriodYearOutOfRange(int minYear, int maxYear) =>
        $"Год должен быть 0 (за все время) или от {minYear} до {maxYear}";

    /// <summary>The month is not one of the twelve</summary>
    public const string PeriodMonthOutOfRange = "Месяц должен быть от 1 до 12";

    /// <summary>A month was asked of the all-time period</summary>
    public const string PeriodMonthNeedsYear = "Для месяца нужен конкретный год";

    /// <summary>The vote in this poll has already been cast</summary>
    public const string AlreadyVoted = "Вы уже голосовали в этом опросе";
}
