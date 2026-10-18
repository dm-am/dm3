using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Tickets;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbTicket = DM.Infrastructure.Persistence.Entities.Moderation.Ticket;
using DbTicketComment = DM.Infrastructure.Persistence.Entities.Moderation.TicketComment;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The conversation of a ticket and its closing are decided by the database:
/// a comment does not land in a ticket closed beside it, a ticket is closed once,
/// a conversation holds no more than its ceiling, and the queue leaves out the
/// complaints about its reader in the list and in the counters alike. Beside them,
/// the two reads that tell a removed account from the guest, which only the
/// soft-delete filter of a live database can blur.
/// </summary>
/// <remarks>
/// Each of these is a read followed by a write, and under READ COMMITTED a read
/// holds nothing (DATA_STORAGE.md, "Транзакция не снимок"): the service reads an
/// open ticket, another moderator closes it, and the comment the service then
/// writes is a comment in a closed ticket, or its closing is the second one of the
/// same ticket. Only a live database can be asked what a
/// conditional UPDATE does under another writer, which is why these are here and
/// not in the domain tests.
/// </remarks>
public class TicketConversationRacesShould : IntegrationTestBase
{
    /// <summary>
    /// Enough callers that the interleaving the defect needs is reached rather
    /// than left to which of two the scheduler happens to start first.
    /// </summary>
    private const int Parallelism = 8;

    public TicketConversationRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The ticket was read open, closed beside the comment, and the comment is
    /// then refused: nothing written, the status the closing left untouched.
    /// </summary>
    [Fact]
    public async Task RefuseACommentToATicketClosedAfterItWasRead()
    {
        var authorId = await AddUserAsync();
        var ticketId = await AddTicketAsync(authorId, targetId: null, TicketSubtype.Bug);

        using var commenting = DatabaseFixture.Factory.Services.CreateScope();
        var comments = commenting.ServiceProvider.GetRequiredService<ITicketRepository>();
        (await comments.Get(ticketId))!.Status.Should().Be(TicketStatus.WaitingForModeration);

        using (var closing = DatabaseFixture.Factory.Services.CreateScope())
        {
            (await closing.ServiceProvider.GetRequiredService<ITicketRepository>()
                    .Close(ticketId, TicketStatus.Closed, TestConstantsModerator, DateTimeOffset.UtcNow))
                .Should().BeTrue();
        }

        var (outcome, comment) = await comments.AddComment(Comment(ticketId, authorId),
            TicketStatus.WaitingForModeration, TicketTextLimits.MaxComments);

        outcome.Should().Be(TicketCommentOutcome.Closed);
        comment.Should().BeNull();
        await using var db = DatabaseFixture.CreateDbContext();
        (await db.TicketComments.CountAsync(c => c.TicketId == ticketId)).Should().Be(0,
            "a comment does not land in a closed ticket");
        (await db.Tickets.SingleAsync(t => t.TicketId == ticketId)).Status.Should().Be(TicketStatus.Closed,
            "and does not open it again");
    }

    /// <summary>
    /// Of closings sent together, one closes the ticket and the rest close nothing,
    /// so the service behind them refuses the rest and announces the closing once.
    /// </summary>
    [Fact]
    public async Task CloseATicketOnceUnderConcurrentClosings()
    {
        var authorId = await AddUserAsync();
        var ticketId = await AddTicketAsync(authorId, targetId: null, TicketSubtype.Bug);
        var closers = await Task.WhenAll(Enumerable.Range(0, Parallelism).Select(_ => AddUserAsync()));

        var closed = await Task.WhenAll(closers.Select(async closer =>
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ITicketRepository>()
                .Close(ticketId, TicketStatus.Spam, closer, DateTimeOffset.UtcNow);
        }));

        closed.Count(won => won).Should().Be(1, "a ticket is closed once, by whoever came first");
        await using var db = DatabaseFixture.CreateDbContext();
        var ticket = await db.Tickets.SingleAsync(t => t.TicketId == ticketId);
        ticket.ResolvedById.Should().Be(closers[Array.IndexOf(closed, true)],
            "the ticket names the moderator whose closing went through");
    }

    /// <summary>
    /// Comments sent together to a conversation near its ceiling: as many land as
    /// there is room for, and the status moves with each of them.
    /// </summary>
    [Fact]
    public async Task TakeNoCommentPastTheCeilingUnderConcurrentWrites()
    {
        const int ceiling = 3;
        var authorId = await AddUserAsync();
        var ticketId = await AddTicketAsync(authorId, targetId: null, TicketSubtype.Bug);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, Parallelism).Select(async _ =>
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var (outcome, _) = await scope.ServiceProvider.GetRequiredService<ITicketRepository>()
                .AddComment(Comment(ticketId, authorId), TicketStatus.WaitingForModeration, ceiling);
            return outcome;
        }));

        outcomes.Count(o => o == TicketCommentOutcome.Added).Should().Be(ceiling);
        outcomes.Count(o => o == TicketCommentOutcome.Full).Should().Be(Parallelism - ceiling);
        await using var db = DatabaseFixture.CreateDbContext();
        (await db.TicketComments.CountAsync(c => c.TicketId == ticketId)).Should().Be(ceiling,
            "every comment waits for the one before it on the row of the ticket, and counts after it");
    }

    /// <summary>
    /// A comment that lands moves the ticket to the status asked for and comes
    /// back as the conversation reads it.
    /// </summary>
    [Fact]
    public async Task MoveTheTicketWithTheCommentThatLands()
    {
        var authorId = await AddUserAsync();
        var ticketId = await AddTicketAsync(authorId, targetId: null, TicketSubtype.Bug);
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();

        var (outcome, comment) = await repository.AddComment(Comment(ticketId, TestConstantsModerator),
            TicketStatus.WaitingForUser, TicketTextLimits.MaxComments);

        outcome.Should().Be(TicketCommentOutcome.Added);
        comment!.Author!.UserId.Should().Be(TestConstantsModerator);
        comment.IsGuest.Should().BeFalse();
        var ticket = await repository.Get(ticketId);
        ticket!.Status.Should().Be(TicketStatus.WaitingForUser);
        ticket.CommentsCount.Should().Be(1);
    }

    /// <summary>
    /// A comment that ends the ticket closes it in the name of its author, in the
    /// statement that moves the status: the last word and the closing land
    /// together, and of such answers sent at once one lands and the rest find the
    /// ticket closed, with nothing of theirs written.
    /// </summary>
    [Fact]
    public async Task CloseTheTicketOnceWithTheCommentThatEndsIt()
    {
        var authorId = await AddUserAsync();
        var ticketId = await AddTicketAsync(authorId, targetId: null, TicketSubtype.Bug);
        var closers = await Task.WhenAll(Enumerable.Range(0, Parallelism).Select(_ => AddUserAsync()));

        var outcomes = await Task.WhenAll(closers.Select(async closer =>
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var (outcome, _) = await scope.ServiceProvider.GetRequiredService<ITicketRepository>()
                .AddComment(Comment(ticketId, closer), TicketStatus.Closed, TicketTextLimits.MaxComments);
            return outcome;
        }));

        outcomes.Count(o => o == TicketCommentOutcome.Added).Should().Be(1, "a ticket is closed once");
        outcomes.Count(o => o == TicketCommentOutcome.Closed).Should().Be(Parallelism - 1);
        await using var db = DatabaseFixture.CreateDbContext();
        var ticket = await db.Tickets.SingleAsync(t => t.TicketId == ticketId);
        var lastWord = await db.TicketComments.SingleAsync(c => c.TicketId == ticketId);
        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ResolvedById.Should().Be(lastWord.AuthorId!.Value,
            "the ticket names the moderator whose last word closed it");
        ticket.ResolvedUtc.Should().NotBeNull();
    }

    /// <summary>
    /// A comment that passes the turn closes nothing: the ticket names nobody as
    /// the one who closed it.
    /// </summary>
    [Fact]
    public async Task CloseNothingWithACommentThatPassesTheTurn()
    {
        var authorId = await AddUserAsync();
        var ticketId = await AddTicketAsync(authorId, targetId: null, TicketSubtype.Bug);
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();

        await repository.AddComment(Comment(ticketId, TestConstantsModerator),
            TicketStatus.WaitingForUser, TicketTextLimits.MaxComments);

        await using var db = DatabaseFixture.CreateDbContext();
        var ticket = await db.Tickets.SingleAsync(t => t.TicketId == ticketId);
        ticket.ResolvedById.Should().BeNull();
        ticket.ResolvedUtc.Should().BeNull();
    }

    /// <summary>
    /// The comment of a removed account stays in the conversation signed by nobody,
    /// and is not taken for the guest's: the guest is the comment without an author
    /// at all.
    /// </summary>
    [Fact]
    public async Task TellTheCommentOfARemovedAccountFromTheGuests()
    {
        var removedId = await AddUserAsync();
        var ticketId = await AddTicketAsync(reporterId: null, targetId: null, TicketSubtype.AccessRecovery);
        var at = DateTimeOffset.UtcNow;
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.TicketComments.AddRange(
                new DbTicketComment
                {
                    TicketCommentId = Guid.NewGuid(),
                    TicketId = ticketId,
                    AuthorId = null,
                    Text = "Гость",
                    CreatedUtc = at
                },
                new DbTicketComment
                {
                    TicketCommentId = Guid.NewGuid(),
                    TicketId = ticketId,
                    AuthorId = removedId,
                    Text = "Удаленный",
                    CreatedUtc = at.AddMinutes(1)
                });
            await db.SaveChangesAsync();
            await db.Users.Where(u => u.UserId == removedId)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsRemoved, true));
        }

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var comments = await scope.ServiceProvider.GetRequiredService<ITicketRepository>().GetComments(ticketId);

        comments.Select(c => c.Text).Should().Equal(["Гость", "Удаленный"],
            "the conversation keeps every comment, in the order it was written");
        comments[0].IsGuest.Should().BeTrue();
        comments[0].Author.Should().BeNull();
        comments[1].IsGuest.Should().BeFalse("a removed account is not the guest who filed the ticket");
        comments[1].Author.Should().BeNull("nothing of a removed account is shown");
    }

    /// <summary>
    /// The ticket of a removed account is not taken for a guest's either: its
    /// signature says "Аккаунт удален", the guest's says "гость", and both have no
    /// reporter to show.
    /// </summary>
    [Fact]
    public async Task TellTheTicketOfARemovedAccountFromTheGuests()
    {
        var removedId = await AddUserAsync();
        var guestTicketId = await AddTicketAsync(reporterId: null, targetId: null, TicketSubtype.AccessRecovery);
        var removedTicketId = await AddTicketAsync(removedId, targetId: null, TicketSubtype.Bug);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            await db.Users.Where(u => u.UserId == removedId)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsRemoved, true));
        }

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var guestTicket = await repository.Get(guestTicketId);
        var removedTicket = await repository.Get(removedTicketId);

        guestTicket!.IsGuest.Should().BeTrue();
        guestTicket.Reporter.Should().BeNull();
        removedTicket.Should().NotBeNull("the ticket of a removed account stays");
        removedTicket!.IsGuest.Should().BeFalse("a removed account is not a guest");
        removedTicket.Reporter.Should().BeNull("nothing of a removed account is shown");
    }

    /// <summary>
    /// The queue of a moderator leaves out the complaints that name him, and its
    /// counters do too: a counter that counts a ticket the list does not show sends
    /// the moderator looking for a ticket that is not there.
    /// </summary>
    [Fact]
    public async Task LeaveTheComplaintsAboutTheReaderOutOfTheQueueAndItsCounters()
    {
        var reporterId = await AddUserAsync();
        var readerId = await AddUserAsync();
        var otherId = await AddUserAsync();
        var aboutReader = await AddTicketAsync(reporterId, readerId, TicketSubtype.PersonalData);
        await AddTicketAsync(reporterId, readerId, TicketSubtype.PersonalData);
        var aboutOther = await AddTicketAsync(reporterId, otherId, TicketSubtype.PersonalData);
        var subtypes = new[] { TicketSubtype.PersonalData };

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var (queue, paging) = await repository.GetTickets(new PagingQuery { Take = 100 },
            TicketStatus.WaitingForModeration, subtypes, readerId);
        var all = await repository.GetTicketCounts(subtypes);
        var counted = await repository.GetTicketCounts(subtypes, readerId);

        var listed = queue.Select(t => t.TicketId).ToArray();
        listed.Should().Contain(aboutOther).And.NotContain(aboutReader);
        all[TicketStatus.WaitingForModeration].Should().Be(counted[TicketStatus.WaitingForModeration] + 2,
            "the two complaints about the reader are not his to count");
        counted[TicketStatus.WaitingForModeration].Should().Be(paging.TotalEntitiesCount,
            "the counter of the queue is the length of the queue");
    }

    private static readonly Guid TestConstantsModerator = TestConstants.ModeratorUserId;

    private static CreateTicketCommentEntity Comment(Guid ticketId, Guid? authorId) => new()
    {
        TicketCommentId = Guid.NewGuid(),
        TicketId = ticketId,
        AuthorId = authorId,
        Text = "Комментарий",
        CreatedUtc = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private async Task<Guid> AddUserAsync()
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is truncated.
            Username = $"tcr{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> AddTicketAsync(Guid? reporterId, Guid? targetId, TicketSubtype subtype)
    {
        var ticketId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Tickets.Add(new DbTicket
        {
            TicketId = ticketId,
            UserId = reporterId,
            TargetId = targetId,
            GuestEmail = reporterId == null ? "guest@example.com" : null,
            EntityType = "",
            Status = TicketStatus.WaitingForModeration,
            Subtype = subtype,
            CreatedUtc = DateTimeOffset.UtcNow,
            Description = "Обращение",
            Comment = ""
        });
        await db.SaveChangesAsync();
        return ticketId;
    }
}
