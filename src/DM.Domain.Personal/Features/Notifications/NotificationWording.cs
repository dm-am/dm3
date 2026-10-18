using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// The words a notification is told in, and the reading of its metadata bag, for
/// every reader of one: the letter, the two bots and the list in the application.
/// </summary>
/// <remarks>
/// Email and the bots say the same thing about the same event, so they say it
/// from one place. While each channel kept a private copy, the copies drifted:
/// the email formatter knew the IsReminder metadata key and the bot formatter
/// did not, and nothing short of a diff of the two files could reveal it. The list
/// in the application kept a third copy of its own on the client, which knew
/// twenty-four of the events and called the rest "Уведомление". The table lives in
/// the domain so that the dispatcher and the API read the same one.
///
/// This is text, not policy: which events reach which channel is decided by
/// NotificationCategoryMapper beside it, which is why the two stay separate
/// tables rather than one.
///
/// The mapper also sets how far the table has to reach. Both senders stop where
/// it answers null, so an event that has a category is an event that will be
/// titled from here. Four of them had a category and no title and went out
/// headed "Уведомление", with the metadata under it and nothing naming what had
/// happened. NotificationTitleCoverageShould in DM.Architecture.Tests keeps the
/// two in step.
///
/// What a channel does with the words is the channel's: the dispatcher escapes
/// them for the markup of a letter and of Telegram, and nothing here knows HTML.
/// </remarks>
public static class NotificationWording
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
        [EventType.GameInactivityWarning] = "Предупреждение о простое игры",
        [EventType.GameRecruitmentOpened] = "Открыт набор в игру",
        [EventType.NewCharacter] = "Новая заявка на персонажа",
        [EventType.StatusCharacterAccepted] = "Персонаж принят",
        [EventType.StatusCharacterDeclined] = "Персонаж отклонен",
        [EventType.StatusCharacterExiled] = "Персонаж изгнан",
        [EventType.StatusCharacterRetired] = "Персонаж выбыл из игры",
        [EventType.StatusCharacterDied] = "Персонаж погиб",
        [EventType.StatusCharacterResurrected] = "Персонаж воскрешен",
        [EventType.StatusCharacterReadmitted] = "Персонаж возвращен в игру",
        [EventType.StatusCharacterLeft] = "Персонаж покинул игру",
        [EventType.AssignmentRequestCreated] = "Приглашение стать ассистентом",
        [EventType.PlayerInvitationCreated] = "Приглашение в игру",
        [EventType.ReaderInvitationCreated] = "Приглашение стать читателем",
        [EventType.RoomPendencyCreated] = "Ожидание поста",
        [EventType.RoomPendencyReminder] = "Напоминание о посте",
        [EventType.RoomPendencyFulfilled] = "Ожидаемый пост написан",
        [EventType.PostReviewed] = "Пост оценен",
        [EventType.LikedGameComment] = "Лайк на комментарий к игре",

        // Forum
        [EventType.NewTopic] = "Новый топик на форуме",
        [EventType.ChangedTopic] = "Топик изменен",
        [EventType.LikedTopic] = "Лайк на топик",
        [EventType.NewTopicComment] = "Новый комментарий в топике",
        [EventType.LikedTopicComment] = "Лайк на комментарий",

        // Blog
        [EventType.NewPublication] = "Новая публикация",
        [EventType.ChangedPublication] = "Публикация изменена",
        [EventType.LikedPublication] = "Лайк на публикацию",
        [EventType.NewBlogComment] = "Новый комментарий в блоге",
        [EventType.LikedBlogComment] = "Лайк на комментарий в блоге",
        [EventType.NewPublicationComment] = "Новый комментарий к публикации",
        [EventType.LikedPublicationComment] = "Лайк на комментарий к публикации",
        [EventType.StatusBlogActive] = "Блог открыт",
        [EventType.StatusBlogClosed] = "Блог закрыт",
        [EventType.StatusBlogFrozen] = "Блог заморожен",
        [EventType.StatusBlogFinished] = "Блог завершен",
        [EventType.BlogInvitationCreated] = "Приглашение в блог",

        // Messages
        [EventType.NewMessage] = "Новое сообщение",
        [EventType.LikedMessage] = "Лайк на сообщение",

        // Subscriptions
        [EventType.NewCommentInSubscribedTopic] = "Новый комментарий в подписанном топике",
        [EventType.NewGameFromSubscribedAuthor] = "Новая игра от подписанного автора",
        [EventType.NewBlogFromSubscribedAuthor] = "Новый блог от подписанного автора",
        [EventType.NewTopicFromSubscribedAuthor] = "Новый топик от подписанного автора",
        [EventType.NewPostInSubscribedGame] = "Новый пост в подписанной игре",

        // Security
        [EventType.PasswordChanged] = "Пароль изменен",
        [EventType.EmailChanged] = "Почта изменена",
        [EventType.SuspiciousLoginActivity] = "Подозрительная активность входа",
        [EventType.AccountLocked] = "Вход в аккаунт временно заблокирован",

        // Moderation
        [EventType.TicketCreated] = "Новое обращение",
        [EventType.TicketAnswered] = "Модерация ответила на обращение",
        [EventType.TicketReplied] = "Автор ответил в обращении",
        [EventType.TicketResolved] = "Обращение закрыто",
        [EventType.WarningIssued] = "Вынесено предупреждение",
        [EventType.WarningPointsChanged] = "Баллы предупреждения изменены",
        [EventType.BanIssued] = "Выдан бан",
        [EventType.BanLifted] = "Бан снят",
        [EventType.AwardGranted] = "Награда выдана"
    };

    /// <summary>
    /// Title of a notification about the given event.
    /// </summary>
    public static string GetTitle(EventType eventType) =>
        EventTypeTitles.TryGetValue(eventType, out var title) ? title : UnknownEventTitle;

    /// <summary>
    /// What a metadata key is called when a channel writes it out.
    /// </summary>
    /// <remarks>
    /// The word is the one the interface already uses for the same thing, by the
    /// rule in CODE_STYLE: a reader shown "Игра" on the page must not be shown
    /// "Название игры" in the letter about it.
    ///
    /// Keys are matched whatever their case, here and in every list below. One bag
    /// has two spellings: the generator writes GameTitle and the store hands the
    /// same bag back as gameTitle (NotificationRepository writes it with the web
    /// defaults), and a reader of the stored bag matched by the exact spelling found
    /// no label, no hidden field and no identifier in it.
    /// </remarks>
    private static readonly Dictionary<string, string> PropertyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        // People
        ["AuthorUsername"] = "Автор",
        ["MasterUsername"] = "Мастер",
        ["ModeratorUsername"] = "Модератор",
        ["TargetUsername"] = "На пользователя",
        ["InviterUsername"] = "Пригласил",
        ["CreatedByUsername"] = "Создал",
        ["FulfilledByUsername"] = "Выполнил",
        ["GrantedByUsername"] = "Вручил",
        ["LikerUsername"] = "Лайк от",
        ["PointsChangedByUsername"] = "Изменил",

        // Games
        ["GameTitle"] = "Игра",
        ["RoomTitle"] = "Комната",
        ["CharacterName"] = "Персонаж",
        ["DaysPending"] = "Дней ожидания",
        ["IsReminder"] = "Напоминание",

        // Forum
        ["TopicTitle"] = "Топик",
        ["BoardTitle"] = "Раздел",

        // Blog
        ["BlogTitle"] = "Блог",
        ["PublicationTitle"] = "Публикация",
        ["Role"] = "Роль",

        // Awards
        ["AwardTitle"] = "Награда",
        ["AwardDescription"] = "Описание",
        ["AwardedUtc"] = "Вручено",
        ["ContestNumber"] = "Номер конкурса",
        ["ContestYear"] = "Год конкурса",

        // Moderation
        ["Description"] = "Описание",
        ["Reason"] = "Причина",
        ["Points"] = "Баллы",
        // The two halves of a corrected warning, named the way the moderation
        // screens name them: the dialog that changes the points calls its fields
        // "Баллы" and "Причина изменения", and the card shows the number the
        // warning was issued with beside the one it carries now.
        ["OriginalPoints"] = "Баллы при выдаче",
        ["PointsChangeText"] = "Причина изменения",
        ["StartedUtc"] = "Начало",
        ["EndedUtc"] = "Окончание",

        // Security
        ["NewEmail"] = "Новая почта",
        ["EventTime"] = "Время",

        // Everywhere
        ["CreatedUtc"] = "Создано"
    };

    /// <summary>
    /// Metadata every channel carries and none of them writes out.
    /// </summary>
    /// <remarks>
    /// Three kinds, none of them addressed to a person. An identifier is what a
    /// notification is turned into a link with — NotificationDestination beside
    /// this file reads these fields for the letter, the two bot messages and the
    /// list alike — and each one is a slug or a raw guid standing next to the human
    /// name of the very same thing:
    /// "Игра: poterjannye-hroniki~aBc" above "Название игры: Потерянные хроники".
    /// A discriminator repeats in English what the title has already said in
    /// Russian: NewStatus arrives as "Frozen" under "Игра заморожена",
    /// InvitationType as "player" under "Приглашение в игру". A rendering hint has
    /// no words at all: the award tier is a number the interface paints a colour
    /// with, the icon name is a file. All of them stay in the payload, because
    /// that is what the link and the tile are built from.
    /// </remarks>
    private static readonly HashSet<string> HiddenProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        // The addressee themselves. Username is the name of the reader the letter
        // is going to, carried so the link can narrow the log to them, and a
        // letter that opens by telling a person their own name says nothing: all
        // eight payloads that carry it - the ban, the warning, the correction of
        // its points, the award and the four security events - address the very
        // person it names.
        "Username",

        // Identifiers: the link is made of them, the sentence is not
        "GameId", "RoomId", "CharacterId", "PendencyId",
        "TopicId", "BoardId", "CommentId",
        "BlogId", "PublicationId",
        "ChatId", "MessageId", "AuthorUserId", "GlobalChatEventId",
        "TokenId", "BanId", "WarningId", "TicketId", "UserAwardId",

        // Machine values: the title says the same thing in words, or there are none
        "NewStatus", "InvitationType", "AwardTier", "IconName"
    };

    /// <summary>
    /// Times a letter prints and a row of the list does not, because the row stands
    /// under a date of its own and these are that date again.
    /// </summary>
    private static readonly string[] RowTimeProperties = ["CreatedUtc", "EventTime"];

    /// <summary>
    /// Times that are moments of their own: the start and the end of a ban, the day
    /// an award was handed out. The list formats them on the reader's clock.
    /// </summary>
    private static readonly string[] MomentProperties = ["StartedUtc", "EndedUtc", "AwardedUtc"];

    /// <summary>
    /// Label of a notification metadata key. An unknown key is shown as it arrived.
    /// </summary>
    public static string FormatPropertyName(string name) =>
        PropertyLabels.TryGetValue(name, out var label) ? label : name;

    /// <summary>
    /// Whether a metadata key is carried for the recipient's client rather than
    /// for the recipient.
    /// </summary>
    public static bool IsHiddenFromText(string name) => HiddenProperties.Contains(name);

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

    /// <summary>
    /// Metadata of a notification as label and value pairs, in the order the generator
    /// declared them. A value that renders to nothing is left out, and so is a field
    /// the reader has no use for.
    /// </summary>
    /// <remarks>
    /// The bag serves two readers at once — the sentence, which has no use for a slug
    /// or a raw guid, and the link, which is made of nothing else. Dropping the
    /// machine-facing fields here rather than in the generators is what keeps the link
    /// buildable: NotificationDestination reads the same bag through ReadFields below.
    /// </remarks>
    /// <param name="metadata">Metadata bag of a notification</param>
    public static IReadOnlyList<KeyValuePair<string, string>> ReadMetadata(object? metadata)
    {
        var shown = new List<KeyValuePair<string, string>>();
        foreach (var (name, value) in ReadFields(metadata))
        {
            if (IsHiddenFromText(name) || string.IsNullOrEmpty(value))
            {
                continue;
            }

            shown.Add(new KeyValuePair<string, string>(FormatPropertyName(name), value));
        }

        return shown;
    }

    /// <summary>
    /// Metadata of a notification as the list in the application shows it under the
    /// title: what a letter prints, less the time the row already shows, with every
    /// other time marked as a moment.
    /// </summary>
    /// <param name="metadata">Metadata bag of a notification</param>
    public static IReadOnlyList<NotificationDetail> ReadDetails(object? metadata)
    {
        var details = new List<NotificationDetail>();
        foreach (var (name, value) in ReadFields(metadata))
        {
            if (IsHiddenFromText(name) || string.IsNullOrEmpty(value) ||
                RowTimeProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            details.Add(new NotificationDetail(
                FormatPropertyName(name),
                value,
                MomentProperties.Contains(name, StringComparer.OrdinalIgnoreCase)));
        }

        return details;
    }

    /// <summary>
    /// Metadata of a notification as it arrived: every field, under the name the
    /// generator declared it by, in the order it declared them.
    /// </summary>
    /// <remarks>
    /// Reading the bag was written out once per channel and the copies differed down
    /// to their serializer options. Metadata that is not an object yields nothing: the
    /// email sender used to fall back to dumping the serialized bag into the letter,
    /// which is both the JSON body EmailTemplatesShould exists to prevent and one more
    /// place the values reached the reader unescaped.
    ///
    /// Nothing is dropped here, because the two readers of the bag drop different
    /// things: the sentence leaves out the identifiers, and the link is one of them.
    /// </remarks>
    /// <param name="metadata">Metadata bag of a notification</param>
    public static IReadOnlyList<KeyValuePair<string, string>> ReadFields(object? metadata)
    {
        if (metadata == null)
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(metadata));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            var fields = new List<KeyValuePair<string, string>>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                fields.Add(new KeyValuePair<string, string>(
                    property.Name, FormatPropertyValue(property.Value)));
            }

            return fields;
        }
        catch (Exception)
        {
            // A bag that cannot be read costs the notification its details, not its
            // delivery.
            return Array.Empty<KeyValuePair<string, string>>();
        }
    }
}
