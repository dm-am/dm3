using System.Collections.Generic;
using System.Text.Json;
using DM.Domain.Core.Enums;

namespace DM.Workers.NotificationDispatcher.Implementation;

/// <summary>
/// Wording shared by every outbound notification channel.
/// </summary>
/// <remarks>
/// Email and the bots say the same thing about the same event, so they say it
/// from one place. While each channel kept a private copy, the copies drifted:
/// the email formatter knew the IsReminder metadata key and the bot formatter
/// did not, and nothing short of a diff of the two files could reveal it.
///
/// This is text, not policy: which events reach which channel is decided by
/// NotificationCategoryMapper in the domain, which is why the table lives here
/// in the worker instead of being merged into the mapper.
/// </remarks>
internal static class NotificationText
{
    /// <summary>
    /// Shown for an event type that carries no title of its own.
    /// </summary>
    private const string UnknownEventTitle = "Уведомление";

    private static readonly Dictionary<EventType, string> EventTypeTitles = new()
    {
        // Games
        [EventType.StatusGameActive] = "Игра началась",
        [EventType.StatusGameClosed] = "Игра закрыта",
        [EventType.StatusGameFrozen] = "Игра заморожена",
        [EventType.StatusGameFinished] = "Игра завершена",
        [EventType.GameClosureWarning] = "Предупреждение о закрытии игры",
        [EventType.GameRecruitmentOpened] = "Открыт набор в игру",
        [EventType.NewCharacter] = "Новая заявка на персонажа",
        [EventType.StatusCharacterAccepted] = "Персонаж принят",
        [EventType.StatusCharacterDeclined] = "Персонаж отклонен",
        [EventType.StatusCharacterExiled] = "Персонаж изгнан",
        [EventType.StatusCharacterRetired] = "Персонаж выбыл из игры",
        [EventType.StatusCharacterDied] = "Персонаж погиб",
        [EventType.StatusCharacterResurrected] = "Персонаж воскрешен",
        [EventType.StatusCharacterLeft] = "Персонаж покинул игру",
        [EventType.StatusCharacterReturned] = "Персонаж вернулся в игру",
        [EventType.AssignmentRequestCreated] = "Приглашение стать ассистентом",
        [EventType.PlayerInvitationCreated] = "Приглашение в игру",
        [EventType.ReaderInvitationCreated] = "Приглашение стать читателем",
        [EventType.RoomPendencyCreated] = "Ожидание поста",
        [EventType.RoomPendencyReminder] = "Напоминание о посте",
        [EventType.PostReviewed] = "Пост оценен",

        // Forum
        [EventType.NewTopic] = "Новый топик на форуме",
        [EventType.LikedTopic] = "Лайк на топик",
        [EventType.NewTopicComment] = "Новый комментарий в топике",
        [EventType.LikedTopicComment] = "Лайк на комментарий",

        // Blog
        [EventType.NewPublication] = "Новая публикация",
        [EventType.LikedPublication] = "Лайк на публикацию",
        [EventType.NewBlogComment] = "Новый комментарий в блоге",
        [EventType.LikedBlogComment] = "Лайк на комментарий в блоге",
        [EventType.NewPublicationComment] = "Новый комментарий к публикации",
        [EventType.LikedPublicationComment] = "Лайк на комментарий к публикации",
        [EventType.StatusBlogActive] = "Блог открыт",
        [EventType.StatusBlogClosed] = "Блог закрыт",
        [EventType.StatusBlogFrozen] = "Блог заморожен",
        [EventType.StatusBlogFinished] = "Блог завершен",

        // Messages
        [EventType.NewMessage] = "Новое сообщение",
        [EventType.LikedMessage] = "Лайк на сообщение",

        // Subscriptions
        [EventType.NewCommentInSubscribedTopic] = "Новый комментарий в подписанной теме",
        [EventType.NewGameFromSubscribedAuthor] = "Новая игра от подписанного автора",
        [EventType.NewBlogFromSubscribedAuthor] = "Новый блог от подписанного автора",
        [EventType.NewTopicFromSubscribedAuthor] = "Новая тема от подписанного автора",
        [EventType.NewPostInSubscribedGame] = "Новый пост в подписанной игре",

        // Security
        [EventType.PasswordChanged] = "Пароль изменен",
        [EventType.EmailChanged] = "Email изменен",
        [EventType.SuspiciousLoginActivity] = "Подозрительная активность входа",

        // Moderation
        [EventType.WarningIssued] = "Вынесено предупреждение",
        [EventType.BanIssued] = "Выдан бан",
        [EventType.BanLifted] = "Бан снят"
    };

    /// <summary>
    /// Title of a notification about the given event.
    /// </summary>
    public static string GetTitle(EventType eventType) =>
        EventTypeTitles.TryGetValue(eventType, out var title) ? title : UnknownEventTitle;

    /// <summary>
    /// Label of a notification metadata key. An unknown key is shown as it arrived.
    /// </summary>
    public static string FormatPropertyName(string name) => name switch
    {
        "GameId" => "Игра",
        "GameTitle" => "Название игры",
        "RoomId" => "Комната",
        "RoomTitle" => "Название комнаты",
        "CharacterId" => "Персонаж",
        "CharacterName" => "Имя персонажа",
        "TopicId" => "Топик",
        "TopicTitle" => "Название топика",
        "Username" => "Пользователь",
        "AuthorUsername" => "Автор",
        "CreatedByUsername" => "Создал",
        "BlogTitle" => "Блог",
        "PublicationTitle" => "Публикация",
        "DaysPending" => "Дней ожидания",
        "IsReminder" => "Напоминание",
        _ => name
    };

    /// <summary>
    /// Notification metadata value rendered as text.
    /// </summary>
    public static string FormatPropertyValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Number => element.ToString(),
        JsonValueKind.True => "Да",
        JsonValueKind.False => "Нет",
        JsonValueKind.Null => "",
        _ => element.ToString()
    };
}
