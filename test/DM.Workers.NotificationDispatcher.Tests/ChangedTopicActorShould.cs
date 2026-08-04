using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers.Forum;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// The actor of a "topic changed" notification, read off the edit history.
/// </summary>
/// <remarks>
/// ActorId is what the blacklist filter groups by: a notification with no actor
/// is delivered to everybody subscribed, including the people who blocked the
/// person who caused it. The topic row keeps its author and no editor, so this
/// generator has one source for the actor and one only — the TopicEdits history
/// the update path writes. The author is deliberately not a fallback: editing a
/// topic is open to moderators and administrators, and substituting the author
/// would hold the notification against the wrong person's blacklist.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class ChangedTopicActorShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public ChangedTopicActorShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    private static readonly Guid SubscriberId = Guid.Parse("00000000-0000-0000-0000-0000000000f9");

    /// <summary>
    /// Writes a board, an author, an editor and one topic, and returns the topic
    /// and the editor. Every identifier is fresh, so the tests do not see each
    /// other's edits.
    /// </summary>
    private async Task<(Guid TopicId, Guid AuthorId, Guid EditorId)> AddTopicAsync(int topicNumber)
    {
        await using var context = _fixture.CreateContext();

        var authorId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddDays(-1);

        context.Users.AddRange(
            User(authorId, "author-" + topicNumber, created),
            User(editorId, "editor-" + topicNumber, created));
        context.Set<Board>().Add(new Board
        {
            BoardId = boardId,
            Title = "Раздел " + topicNumber,
            Alias = "board-" + topicNumber,
            Order = topicNumber,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.RegularUser
        });
        await context.SaveChangesAsync();

        context.Topics.Add(new Topic
        {
            TopicId = topicId,
            BoardId = boardId,
            TopicNumber = topicNumber,
            AuthorId = authorId,
            Title = "Тема " + topicNumber,
            Text = "Текст темы.",
            CreatedUtc = created,
            IsRemoved = false,
            IsClosed = false,
            IsAttached = false
        });
        await context.SaveChangesAsync();

        return (topicId, authorId, editorId);
    }

    private async Task AddEditAsync(Guid topicId, Guid editorId, DateTimeOffset editedUtc)
    {
        await using var context = _fixture.CreateContext();
        context.TopicEdits.Add(new TopicEdit
        {
            TopicEditId = Guid.NewGuid(),
            TopicId = topicId,
            EditorUserId = editorId,
            EditedUtc = editedUtc
        });
        await context.SaveChangesAsync();
    }

    private async Task<CreateNotification?> ChangedAsync(Guid topicId)
    {
        var repository = new Mock<ISubscriptionRepository>();
        repository
            .Setup(r => r.GetByTargetWithSettingsAsync(
                SubscriptionTargetType.Topic,
                topicId,
                SubscriptionSettings.NewComments,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Subscription>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SubscriberId = SubscriberId,
                    TargetType = SubscriptionTargetType.Topic,
                    TargetId = topicId,
                    Settings = SubscriptionSettings.NewComments
                }
            });

        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new ChangedTopicNotificationGenerator(context, repository.Object), topicId);
        return produced.SingleOrDefault();
    }

    [Fact]
    public async Task NameTheEditorRatherThanTheAuthor()
    {
        var (topicId, authorId, editorId) = await AddTopicAsync(9201);
        await AddEditAsync(topicId, editorId, DateTimeOffset.UtcNow);

        var notification = await ChangedAsync(topicId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(new[] { SubscriberId });
        notification.ActorId.Should().Be(editorId,
            "the blacklist filter groups by the actor, and the actor of a change " +
            "is whoever made it");
        notification.ActorId.Should().NotBe(authorId,
            "the person who opened the topic is not the person who changed it");
    }

    [Fact]
    public async Task NameTheLatestEditorWhenATopicWasChangedTwice()
    {
        var (topicId, _, firstEditorId) = await AddTopicAsync(9202);
        var secondEditorId = Guid.NewGuid();
        await using (var context = _fixture.CreateContext())
        {
            context.Users.Add(User(secondEditorId, "editor-9202-second", DateTimeOffset.UtcNow.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        await AddEditAsync(topicId, firstEditorId, DateTimeOffset.UtcNow.AddHours(-2));
        await AddEditAsync(topicId, secondEditorId, DateTimeOffset.UtcNow);

        var notification = await ChangedAsync(topicId);

        notification.Should().NotBeNull();
        notification!.ActorId.Should().Be(secondEditorId,
            "the event is about the change that just happened, so the history is " +
            "read newest first");
    }

    /// <summary>
    /// A topic nobody has edited yet has no actor, and the notification is
    /// delivered rather than filtered against a person who did nothing.
    /// </summary>
    [Fact]
    public async Task NameNobodyWhenTheTopicHasNoEditHistory()
    {
        var (topicId, _, _) = await AddTopicAsync(9203);

        var notification = await ChangedAsync(topicId);

        notification.Should().NotBeNull();
        notification!.ActorId.Should().BeNull();
        notification.UsersInterested.Should().Equal(new[] { SubscriberId });
    }

    private static User User(Guid id, string username, DateTimeOffset created) => new()
    {
        UserId = id,
        Username = username,
        Email = username + "@example.com",
        PasswordHash = "fakehash",
        Salt = "fakesalt",
        PasswordHashVersion = 2,
        Role = UserRole.RegularUser,
        CreatedUtc = created,
        LastActivityUtc = DateTimeOffset.UtcNow,
        IsRemoved = false,
        Status = string.Empty,
        Name = string.Empty,
        Location = string.Empty,
        Info = string.Empty
    };
}
