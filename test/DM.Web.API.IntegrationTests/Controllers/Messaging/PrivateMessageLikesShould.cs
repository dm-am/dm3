using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// A like on a message of a private conversation is on the message the reader is
/// handed back.
/// </summary>
/// <remarks>
/// The projection of a message is one formula for both surfaces, and it filled no
/// likes at all: every read answered with an empty list however many likes the row
/// had. What that cost is the heart on the page — the client replaces the message
/// it holds with the one the answer brings, so a like landed in the table and went
/// out again on the screen a moment later.
///
/// The global chat's half of the same proof is in
/// <see cref="GlobalChatMessageOperationsShould" />; this is the private one.
/// Written against HTTP rather than the services: the omission lived in a query
/// projection, which every test with a substituted repository steps straight over.
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

    /// <summary>
    /// The seeded message is shared with the tests around this one, so the like is
    /// taken back off it however this one ends.
    /// </summary>
    private Task Unlike() => Client.SendAsync(CreateAuthenticatedRequest(
        HttpMethod.Delete, $"/v1/messages/{TestConstants.TestMessageId}/likes", Reader));

    private static string[] Names(JsonElement message) => message
        .GetProperty("likes")
        .EnumerateArray()
        .Select(l => l.GetProperty("username").GetString()!)
        .ToArray();
}
