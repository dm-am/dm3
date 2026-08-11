using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using DM.Domain.Core.Enums;

namespace DM.Workers.NotificationDispatcher.Implementation;

/// <summary>
/// Wording shared by every outbound notification channel, and the reading and
/// escaping that puts it into a message.
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
///
/// The mapper also sets how far the table has to reach. Both senders stop where
/// it answers null, so an event that has a category is an event that will be
/// titled from here. Four of them had a category and no title and went out
/// headed "Уведомление", with the metadata under it and nothing naming what had
/// happened. NotificationTitleCoverageShould in DM.Architecture.Tests keeps the
/// two in step.
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
        [EventType.GameInactivityWarning] = "Предупреждение о простое игры",
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
        [EventType.WarningIssued] = "Вынесено предупреждение",
        [EventType.BanIssued] = "Выдан бан",
        [EventType.BanLifted] = "Бан снят",
        [EventType.AwardGranted] = "Награда выдана"
    };

    /// <summary>
    /// Encoder every value that becomes markup goes through.
    /// </summary>
    /// <remarks>
    /// The same one the Razor templates of DM.Infrastructure.Mail render through, and
    /// for the same reason: the default encoder turns every letter of a Russian name
    /// into a numeric reference. All of Unicode is allowed through, angle brackets,
    /// ampersand and quotes are not. Telegram's HTML parse mode reads what comes out
    /// of it: the four named entities it knows are exactly the ones this encoder
    /// emits, and numeric ones it accepts without exception.
    /// </remarks>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

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
    /// </remarks>
    private static readonly Dictionary<string, string> PropertyLabels = new()
    {
        // People
        ["Username"] = "Пользователь",
        ["AuthorUsername"] = "Автор",
        ["MasterUsername"] = "Мастер",
        ["ModeratorUsername"] = "Модератор",
        ["TargetUsername"] = "На пользователя",
        ["InviterUsername"] = "Пригласил",
        ["CreatedByUsername"] = "Создал",
        ["FulfilledByUsername"] = "Выполнил",
        ["GrantedByUsername"] = "Вручил",
        ["LikerUsername"] = "Лайк от",

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
    /// Three kinds, none of them addressed to a person. An identifier is what the
    /// notification in the app is turned into a link with — the client reads
    /// GameId, TopicId, BlogId out of this same payload — and each one is a slug
    /// or a raw guid standing next to the human name of the very same thing:
    /// "Игра: poterjannye-hroniki~aBc" above "Название игры: Потерянные хроники".
    /// A discriminator repeats in English what the title has already said in
    /// Russian: NewStatus arrives as "Frozen" under "Игра заморожена",
    /// InvitationType as "player" under "Приглашение в игру". A rendering hint has
    /// no words at all: the award tier is a number the interface paints a colour
    /// with, the icon name is a file. All of them stay in the payload, because
    /// that is what the link and the tile are built from.
    /// </remarks>
    private static readonly HashSet<string> HiddenProperties = new()
    {
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
    /// Reading the bag was written out once per channel and the copies differed down
    /// to their serializer options. Metadata that is not an object yields nothing: the
    /// email sender used to fall back to dumping the serialized bag into the letter,
    /// which is both the JSON body EmailTemplatesShould exists to prevent and one more
    /// place the values reached the reader unescaped.
    ///
    /// The bag serves two readers at once — the client, which turns identifiers into a
    /// link, and the person, who has no use for a slug or a raw guid. Dropping the
    /// machine-facing fields here rather than in the generators keeps the link working.
    /// </remarks>
    /// <param name="metadata">Metadata bag of a notification</param>
    public static IReadOnlyList<KeyValuePair<string, string>> ReadMetadata(object? metadata)
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
                if (IsHiddenFromText(property.Name))
                {
                    continue;
                }

                var value = FormatPropertyValue(property.Value);
                if (!string.IsNullOrEmpty(value))
                {
                    fields.Add(new KeyValuePair<string, string>(FormatPropertyName(property.Name), value));
                }
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

    /// <summary>
    /// Text on its way into HTML: the body of the letter and the Telegram message.
    /// </summary>
    /// <param name="text">Text that has to arrive as text</param>
    public static string EscapeHtml(string text) => Encoder.Encode(text);
}
