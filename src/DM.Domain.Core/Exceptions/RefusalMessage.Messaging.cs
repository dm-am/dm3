namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusals of chats, messages and global chat events
/// </summary>
public static partial class RefusalMessage
{
    // Messaging

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
}
