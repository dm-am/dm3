using System.Net;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Messaging;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using DM.Web.API.IntegrationTests.Helpers;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

// Private correspondence is not moderated: a moderator outside it reads none of it, and a removed
// message of it reaches nobody, the moderator included.
public class ModeratorCorrespondenceShould : IntegrationTestBase
{
    private const string LiveText = "A live private message.";
    private const string RemovedText = "A removed private message.";

    public ModeratorCorrespondenceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GiveAModeratorOutsideTheCorrespondenceNothingOfIt()
    {
        var (chatId, liveId, removedId) = await CreateCorrespondence();
        try
        {
            var moderator = CustomWebApplicationFactory.CreateModeratorUser();

            foreach (var url in new[]
                     {
                         $"/v1/chats/{chatId}", $"/v1/chats/{chatId}/messages",
                         $"/v1/messages/{liveId}", $"/v1/messages/{removedId}"
                     })
            {
                var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, moderator));
                var body = await response.Content.ReadAsStringAsync();

                response.IsSuccessStatusCode.Should().BeFalse($"{url} is no moderator's to read: {body}");
                body.Should().NotContain(LiveText).And.NotContain(RemovedText);
            }
        }
        finally
        {
            await DropChat(chatId);
        }
    }

    [Fact]
    public async Task GiveTheTextOfARemovedMessageToNobody()
    {
        var (chatId, _, removedId) = await CreateCorrespondence(withModerator: true);
        try
        {
            foreach (var reader in new[]
                     {
                         CustomWebApplicationFactory.CreateTestUser(),
                         CustomWebApplicationFactory.CreateModeratorUser()
                     })
            {
                var feed = await Client.SendAsync(
                    CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/chats/{chatId}/messages", reader));
                var single = await Client.SendAsync(
                    CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/messages/{removedId}", reader));

                feed.StatusCode.Should().Be(HttpStatusCode.OK, reader.Username);
                (await feed.Content.ReadAsStringAsync()).Should().Contain(LiveText).And.NotContain(RemovedText);
                single.StatusCode.Should().Be(HttpStatusCode.NotFound, reader.Username);
                (await single.Content.ReadAsStringAsync()).Should().NotContain(RemovedText);
            }
        }
        finally
        {
            await DropChat(chatId);
        }
    }

    private async Task<(Guid ChatId, Guid LiveId, Guid RemovedId)> CreateCorrespondence(bool withModerator = false)
    {
        var chatId = Guid.NewGuid();
        var liveId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Chats.Add(new Chat { ChatId = chatId, Type = ChatType.Group, Title = $"Correspondence {chatId:N}" });
        var participants = withModerator
            ? new[] { TestConstants.TestUserId, TestConstants.SecondUserId, TestConstants.ModeratorUserId }
            : [TestConstants.TestUserId, TestConstants.SecondUserId];
        foreach (var userId in participants)
        {
            dbContext.Set<UserChatLink>().Add(new UserChatLink
            {
                UserChatLinkId = Guid.NewGuid(),
                ChatId = chatId,
                UserId = userId,
            });
        }

        dbContext.Messages.AddRange(
            new DbMessage
            {
                MessageId = liveId,
                ChatId = chatId,
                UserId = TestConstants.TestUserId,
                CreatedUtc = now.AddMinutes(-2),
                Text = LiveText,
            },
            new DbMessage
            {
                MessageId = removedId,
                ChatId = chatId,
                UserId = TestConstants.SecondUserId,
                CreatedUtc = now.AddMinutes(-1),
                Text = RemovedText,
                IsRemoved = true,
                DeletedByUserId = TestConstants.SecondUserId,
            });

        await dbContext.SaveChangesAsync();
        return (chatId, liveId, removedId);
    }

    private async Task DropChat(Guid chatId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await ChatRows.DropAsync(scope.ServiceProvider.GetRequiredService<DmDbContext>(), chatId);
    }
}
