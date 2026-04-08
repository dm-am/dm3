using System.Net;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// Integration tests for NotificationController
/// </summary>
public class NotificationControllerShould : IntegrationTestBase
{
    public NotificationControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

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
