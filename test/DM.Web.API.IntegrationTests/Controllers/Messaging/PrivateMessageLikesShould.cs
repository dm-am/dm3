using System.Net;
using System.Text.Json;
using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <remarks>
/// The client replaces the message with the one the answer brings. The global chat's half is
/// <see cref="GlobalChatMessageOperationsShould" />; over HTTP, the likers are filled in a projection.
/// </remarks>
public class PrivateMessageLikesShould : IntegrationTestBase
{
    public PrivateMessageLikesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Author => CustomWebApplicationFactory.CreateTestUser();
    private static GeneralUser Reader => CustomWebApplicationFactory.CreateSecondUser();

    [Fact]
    public async Task CarryTheLikeOnTheListedMessage()
    {
        await Like();
        try
        {
            var listing = await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/chats/{TestConstants.TestChatId}/messages?limit=100", Author));

            listing.StatusCode.Should().Be(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
            var message = document.RootElement
                .GetProperty("resources")
                .EnumerateArray()
                .Single(m => m.GetProperty("id").GetGuid() == TestConstants.TestMessageId);

            Names(message).Should().Contain(Reader.Username);
        }
        finally
        {
            await Unlike();
        }
    }

    [Fact]
    public async Task CarryTheLikeOnTheAnswerToTheLike()
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/messages/{TestConstants.TestMessageId}/likes", Reader));
        try
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Names(document.RootElement.GetProperty("resource")).Should().Contain(Reader.Username,
                "the answer replaces the message on the page, and the like has to be on it");
        }
        finally
        {
            await Unlike();
        }
    }

    private async Task Like()
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/messages/{TestConstants.TestMessageId}/likes", Reader));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The seeded message is shared with the tests around this one, so the like is taken back off it
    // however this one ends.
    private Task Unlike() => Client.SendAsync(CreateAuthenticatedRequest(
        HttpMethod.Delete, $"/v1/messages/{TestConstants.TestMessageId}/likes", Reader));

    private static string[] Names(JsonElement message) => message
        .GetProperty("likes")
        .EnumerateArray()
        .Select(l => l.GetProperty("username").GetString()!)
        .ToArray();
}
