using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DM.Testing;
using AwesomeAssertions;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Personal;

/// <summary>
/// The user list fetches the four numbers it draws, and none of the profile ones.
/// </summary>
/// <remarks>
/// The counters are nullable, so "not fetched" differs from "fetched and zero". Both directions: the
/// profile still answers everything the list does not fetch.
/// </remarks>
public class UserListEnrichmentShould
{
    private static readonly Guid AuthorId = Guid.Parse("3b9d4c10-0000-4000-8000-000000000001");
    private static readonly Guid FanId = Guid.Parse("3b9d4c10-0000-4000-8000-000000000002");

    private static readonly DateTimeOffset Created = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);


    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
        Role = UserRole.RegularUser,
        CreatedUtc = Created,
        LastActivityUtc = Created,
    };

    /// <summary>
    /// One user with something of every kind; the topic and the comment are profile content.
    /// </summary>
    private static DmDbContext Seeded()
    {
        var context = InMemoryStore.Open();

        context.Users.AddRange(NewUser(AuthorId, "author"), NewUser(FanId, "fan"));

        context.Games.Add(new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = "ggame",
            MasterId = AuthorId,
            Title = "Игра",
            CreatedUtc = Created,
        });

        context.Set<DbEndorsement>().Add(new DbEndorsement
        {
            UserEndorsementId = Guid.NewGuid(),
            AuthorId = FanId,
            TargetUserId = AuthorId,
            Text = "Хороший ведущий",
            CreatedUtc = Created,
        });

        var topicId = Guid.NewGuid();
        context.Topics.Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = Guid.NewGuid(),
            AuthorId = AuthorId,
            Title = "Тема",
            Text = "Первое сообщение",
            TopicNumber = 1,
            CreatedUtc = Created,
        });

        context.Comments.Add(new DbComment
        {
            CommentId = Guid.NewGuid(),
            EntityId = topicId,
            AuthorId = AuthorId,
            Text = "Реплика",
            CreatedUtc = Created,
        });

        context.SaveChanges();
        return context;
    }

    private static UserRepository Repository(DmDbContext context) =>
        new(context, new FixedClock(Created.AddDays(1)));

    private static Task<GeneralUser> ListedAsync(DmDbContext context) =>
        Repository(context)
            .GetUsersAsync(
                new PagingData(new PagingQuery { Skip = 0, Take = 50 }, 50, 2),
                new UserFilter { Activity = UserActivityFilter.All })
            .ContinueWith(t => t.Result.Single(u => u.UserId == AuthorId));

    [Fact]
    public async Task CarryTheFourNumbersTheTableDraws()
    {
        using var context = Seeded();

        var listed = await ListedAsync(context);

        listed.GamesHosting.Should().Be(1, "he masters one game, and the column shows it");
        listed.GamesPlaying.Should().Be(0);
        listed.BlogsHosting.Should().Be(0);
        listed.EndorsementsReceivedCount.Should().Be(1,
            "recommendations received is the fourth column, and it is fetched for the list");
    }

    [Fact]
    public async Task LeaveTheProfileCountersAloneOnTheList()
    {
        using var context = Seeded();

        var listed = await ListedAsync(context);

        listed.TopicsAuthoredCount.Should().BeNull(
            "he authored a topic, so a zero here would say the list ran the aggregate and " +
            "found nothing — null says it never ran it, which is the point of the split");
        listed.CommentsAuthoredCount.Should().BeNull();
        listed.PostReviewsReceivedCount.Should().BeNull();
        listed.LikesReceivedCount.Should().BeNull(
            "likes received is four joins on its own, and the table has no column for it");
        listed.BansReceivedCount.Should().BeNull();
        listed.SubscribersByCategory.Should().BeNull(
            "the subscriber summary is a windowed aggregate the profile draws and the list does not");
        listed.UsernameHistory.Should().BeNullOrEmpty(
            "the history is its own statement, and the list shows the current name");
    }

    [Fact]
    public async Task StillAnswerEverythingOnTheProfile()
    {
        using var context = Seeded();

        var profile = (await Repository(context).GetUserAsync(AuthorId))!;

        profile.TopicsAuthoredCount.Should().Be(1, "the profile is what the full enrichment is for");
        profile.CommentsAuthoredCount.Should().Be(1);
        profile.LikesReceivedCount.Should().Be(0, "nobody liked him, and the profile says so");
        profile.GamesHosting.Should().Be(1,
            "the list numbers are the first part of the profile numbers, not an alternative to them");
        profile.EndorsementsReceivedCount.Should().Be(1);
    }
}
