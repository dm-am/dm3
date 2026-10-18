using System.Net;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using AwesomeAssertions;
using Xunit;
using DbNotification = DM.Infrastructure.Persistence.Entities.Personal.Notifications.Notification;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// Integration tests for NotificationController
/// </summary>
public class NotificationControllerShould : IntegrationTestBase
{
    /// <summary>
    /// The bag of an accepted character, spelled the way the store holds it: the
    /// generator writes CharacterName, the store hands back characterName.
    /// </summary>
    private const string AcceptedCharacter = """
        {"characterName":"Цири","characterId":"tsiri~aBcD","gameTitle":"Ведьмак: Дикая Охота",
         "gameId":"vedmak~eFgH-1_2","newStatus":"Active","masterUsername":"SolohinLex"}
        """;

    /// <summary>
    /// The bag of a ban as the store holds it, with the two moments of its own.
    /// </summary>
    private const string IssuedBan = """
        {"banId":"ban~aBcD","username":"Player-Two","reason":"Флуд","moderatorUsername":"TestSeniorMod",
         "endedUtc":"2026-10-19T12:00:00+00:00","startedUtc":"2026-10-12T12:00:00+00:00"}
        """;

    public NotificationControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Reader => CustomWebApplicationFactory.CreateSecondUser();

    /// <summary>
    /// A row of the list says what happened, where it leads, when, and with what.
    /// </summary>
    /// <remarks>
    /// The list used to carry the event and the bag and nothing else, and the page
    /// named the event and built the link out of a table of its own that knew
    /// twenty-four events of fifty-nine. The words and the path come from the
    /// table the letter is written from, read off the stored bag, whose keys are
    /// camelCase where the generator wrote them PascalCase.
    /// </remarks>
    [Fact]
    public async Task ListWhatHappenedWhereItLeadsAndWhen()
    {
        var createdUtc = Now();
        var id = await StoreAsync(EventType.StatusCharacterAccepted, AcceptedCharacter, createdUtc, (Reader, false));

        var row = await ListedAsync(Reader, id);

        Field(row, "title").GetString().Should().Be("Персонаж принят");
        Field(row, "url").GetString().Should().Be("/game/vedmak~eFgH-1_2");
        Field(row, "createdUtc").GetDateTimeOffset().Should().Be(createdUtc);
        Field(row, "isRead").GetBoolean().Should().BeFalse();
        Details(row).Should().BeEquivalentTo(
            new[]
            {
                ("Персонаж", "Цири", false),
                ("Игра", "Ведьмак: Дикая Охота", false),
                ("Мастер", "SolohinLex", false)
            },
            "the details are the fields the letter prints, the identifiers and the status left out");
    }

    /// <summary>
    /// A notification read stays in the list, marked read.
    /// </summary>
    /// <remarks>
    /// The list dropped the flag on the join, so the page could not tell a read
    /// row from an unread one and hid the rows it had marked until the next
    /// reload brought them back.
    /// </remarks>
    [Fact]
    public async Task KeepAReadNotificationInTheListMarkedRead()
    {
        var id = await StoreAsync(EventType.StatusCharacterAccepted, AcceptedCharacter, Now(), (Reader, false));

        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/users/me/notifications/{id}/unread", Reader));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());

        var row = await ListedAsync(Reader, id);
        Field(row, "isRead").GetBoolean().Should().BeTrue("the reader has just marked it read");
    }

    /// <summary>
    /// Whether a notification was read is the reader's own: one row of the store
    /// reaches several people, and each reads it in their own time.
    /// </summary>
    [Fact]
    public async Task AnswerEachReaderWithTheirOwnMark()
    {
        var other = CustomWebApplicationFactory.CreateTestUser();
        var id = await StoreAsync(EventType.StatusCharacterAccepted, AcceptedCharacter, Now(),
            (Reader, false), (other, true));

        Field(await ListedAsync(Reader, id), "isRead").GetBoolean().Should().BeFalse(
            "somebody else having read it says nothing about this reader");
        Field(await ListedAsync(other, id), "isRead").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The start and the end of a ban arrive marked as moments, for the page to
    /// put on the reader's clock, though the store spells them startedUtc and
    /// endedUtc.
    /// </summary>
    [Fact]
    public async Task MarkTheMomentsOfAStoredBag()
    {
        var id = await StoreAsync(EventType.BanIssued, IssuedBan, Now(), (Reader, false));

        var row = await ListedAsync(Reader, id);

        Field(row, "url").GetString().Should().Be("/users/Player-Two");
        Details(row).Should().BeEquivalentTo(new[]
        {
            ("Причина", "Флуд", false),
            ("Модератор", "TestSeniorMod", false),
            ("Окончание", "2026-10-19T12:00:00+00:00", true),
            ("Начало", "2026-10-12T12:00:00+00:00", true)
        });
    }

    /// <summary>
    /// A changed email leads nowhere and does not repeat its own time under the
    /// date of the row, though the store spells the time eventTime.
    /// </summary>
    [Fact]
    public async Task LeaveOutTheTimeTheRowShowsAndLeadNowhereWithoutAPage()
    {
        var id = await StoreAsync(EventType.EmailChanged,
            """{"username":"Player-Two","newEmail":"two@example.com","eventTime":"2026-10-12T12:00:00Z"}""",
            Now(), (Reader, false));

        var row = await ListedAsync(Reader, id);

        Field(row, "title").GetString().Should().Be("Почта изменена");
        row.TryGetProperty("url", out _).Should().BeFalse(
            "a changed email is not a page, and the API leaves a null out: {0}", row.GetRawText());
        Details(row).Should().Equal(new[] { ("Новая почта", "two@example.com", false) });
    }

    /// <summary>
    /// The current moment to the second: the store keeps microseconds, and a row
    /// written now stands at the top of a list sorted newest first.
    /// </summary>
    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero);
    }

    /// <summary>
    /// A notification written the way the store holds one, addressed to the readers given.
    /// </summary>
    private async Task<Guid> StoreAsync(
        EventType eventType, string metadata, DateTimeOffset createdUtc,
        params (GeneralUser User, bool IsRead)[] recipients)
    {
        var id = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<DbNotification>().Add(new DbNotification
        {
            NotificationId = id,
            EventType = eventType,
            CreatedUtc = createdUtc,
            Metadata = metadata,
            Recipients = recipients
                .Select(r => new NotificationRecipient
                {
                    NotificationId = id,
                    UserId = r.User.UserId,
                    IsRead = r.IsRead
                })
                .ToList()
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>The row of the notification in the reader's list.</summary>
    private async Task<JsonElement> ListedAsync(GeneralUser reader, Guid id)
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/notifications?skip=0&take=100", reader));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var rows = document.RootElement.GetProperty("resources")
            .EnumerateArray()
            .Where(row => row.GetProperty("id").GetGuid() == id)
            .Select(row => row.Clone())
            .ToArray();
        rows.Should().ContainSingle("the notification is addressed to this reader: {0}", body);
        return rows[0];
    }

    private static JsonElement Field(JsonElement row, string name)
    {
        row.TryGetProperty(name, out var value).Should().BeTrue(
            "a row of the list carries {0}: {1}", name, row.GetRawText());
        return value;
    }

    private static (string Label, string Value, bool IsMoment)[] Details(JsonElement row) =>
        Field(row, "details")
            .EnumerateArray()
            .Select(detail => (
                detail.GetProperty("label").GetString()!,
                detail.GetProperty("value").GetString()!,
                detail.GetProperty("isMoment").GetBoolean()))
            .ToArray();

    [Fact]
    public async Task GetNotifications_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotifications_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/notifications");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUnreadCount_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me/notifications/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUnreadCount_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/notifications/unread");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MarkNotificationAsRead_RequiresAuthentication()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/users/me/notifications/{notificationId}/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAllNotificationsAsRead_RequiresAuthentication()
    {
        // Act
        var response = await Client.DeleteAsync("/v1/users/me/notifications/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotificationSettings_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me/notifications/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNotificationSettings_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/notifications/settings");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GenerateBotLinkCode_RequiresAuthentication()
    {
        // Act
        var response = await Client.PostAsync("/v1/users/me/notifications/bots/telegram", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DisconnectBot_RequiresAuthentication()
    {
        // Act
        var response = await Client.DeleteAsync("/v1/users/me/notifications/bots/telegram");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
