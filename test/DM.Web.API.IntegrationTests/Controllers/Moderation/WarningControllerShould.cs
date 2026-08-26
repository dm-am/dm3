using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for WarningController
/// </summary>
public class WarningControllerShould : IntegrationTestBase
{
    public WarningControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUserWarnings_PublicView_ReturnsAggregatesWithoutModerationDetails()
    {
        // Arrange - seed directly: the create endpoint requires a moderator
        // identity, while the public GET must be exercised anonymously
        var warningId = Guid.NewGuid();
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Warnings.Add(new DbWarning
            {
                WarningId = warningId,
                TargetUserId = TestConstants.SecondUserId,
                AuthorId = TestConstants.ModeratorUserId,
                EntityId = Guid.Empty,
                EntityType = WarningEntityType.Unknown,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                Text = "Секретная причина модерации",
                Points = 2,
                IsRemoved = false
            });
            await db.SaveChangesAsync();
        }

        try
        {
            // Act - anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/warnings");

            // Assert - aggregates are present, moderation details are not
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("totalPoints").GetInt32().Should().Be(2);
            root.GetProperty("activeCount").GetInt32().Should().Be(1);

            var warning = root.GetProperty("warnings").EnumerateArray().Single();
            warning.GetProperty("points").GetInt32().Should().Be(2);
            warning.GetProperty("isActive").GetBoolean().Should().BeTrue();
            warning.TryGetProperty("createdUtc", out _).Should().BeTrue();
            warning.TryGetProperty("reason", out _).Should().BeFalse();
            warning.TryGetProperty("moderator", out _).Should().BeFalse();
            warning.TryGetProperty("user", out _).Should().BeFalse();
            warning.TryGetProperty("entityId", out _).Should().BeFalse();
            warning.TryGetProperty("entityType", out _).Should().BeFalse();
            json.Should().NotContain("Секретная причина модерации");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Warnings.Where(w => w.WarningId == warningId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task GetWarnings_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/warnings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWarnings_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/warnings");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWarning_RequiresAuthentication()
    {
        // Arrange
        var warningData = new
        {
            userLogin = TestConstants.SecondUserLogin,
            reason = "Misconduct",
            description = "Test warning"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/moderation/warnings", warningData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWarning_RequiresAuthentication()
    {
        // Arrange
        var warningId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/moderation/warnings/{warningId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
