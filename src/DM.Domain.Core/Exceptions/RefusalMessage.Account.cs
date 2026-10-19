using DM.Domain.Core.Users;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusals of signing in, the second factor and the account's own name
/// </summary>
public static partial class RefusalMessage
{
    // Authorization

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
    public const string AccountRemoved = RemovedAccount.Name;

    // Second factor

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

    // Users

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

    // Credentials

    /// <summary>
    /// The address and the password do not go together. One sentence for both
    /// halves, so a caller cannot tell which of them was wrong
    /// </summary>
    public const string WrongCredentials = "Неверная почта или пароль";

    /// <summary>The address belongs to a registration whose link was never followed</summary>
    public const string RegistrationPending = "Регистрация не завершена";

    /// <summary>The sign-in was refused before the credentials were read: the bot field was filled</summary>
    public const string LoginFailed = "Не удалось войти";

    /// <summary>The registration was refused before the address was read: the bot field was filled</summary>
    public const string RegistrationFailed = "Не удалось зарегистрироваться";

    /// <summary>
    /// The address is not one mail could be sent to. Said under the field, by every
    /// form that asks for an address: registration, the change of address, a ticket
    /// </summary>
    public const string EmailInvalid = "Неверный формат почты";

    // Username change

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

    /// <summary>
    /// The password is in a breach corpus. Reaches the reader under the password
    /// field itself, so it says what to do rather than what happened
    /// </summary>
    public const string PasswordBreached =
        "Этот пароль скомпрометирован утечкой, выберите другой";
}
