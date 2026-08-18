using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUsernameChangeRequest = DM.Infrastructure.Persistence.Entities.Account.UsernameChangeRequest;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Expiring an unused approval keeps its explanation, whether or not a moderator
/// left a comment of their own.
/// </summary>
/// <remarks>
/// The pass used to append the reason to whatever comment was already stored:
/// `SetProperty(r => r.ResolverComment, r => r.ResolverComment + suffix)`, and
/// the suffix carried its own separator. Approving takes no comment - only
/// rejection demands one - so that column is normally null here, and what the
/// requester was shown began with a separator joining it to nothing: " | Токен
/// истек...".
///
/// Asserted against a real database on purpose. How a null operand is treated in
/// an ExecuteUpdate concatenation is the provider's decision, not the language's,
/// and it is the only thing under test here.
/// </remarks>
public class UsernameChangeExpiryShould : IntegrationTestBase
{
    private const string Reason = "Токен истек: пользователь не выбрал новое имя в отведенное время";
    private const string ModeratorComment = "Одобрено, имя нейтральное";

    private static readonly DateTimeOffset Approved = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 5, 8, 10, 0, 0, TimeSpan.Zero);

    public UsernameChangeExpiryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The ordinary case: nobody wrote a comment when approving, so the reason is
    /// the whole text and must survive on its own.
    /// </summary>
    [Fact]
    public Task KeepTheReasonWhenTheApprovalCarriedNoComment() =>
        ExpireAndRead(null, comment => comment.Should().Be(Reason));

    /// <summary>
    /// An empty comment is the same case as a missing one, and must not produce a
    /// text that opens with a separator.
    /// </summary>
    [Fact]
    public Task KeepTheReasonWhenTheStoredCommentIsEmpty() =>
        ExpireAndRead(string.Empty, comment => comment.Should().Be(Reason));

    /// <summary>
    /// A moderator who did leave a comment keeps it: the reason joins it rather
    /// than replacing it.
    /// </summary>
    [Fact]
    public Task JoinTheReasonToACommentThatIsAlreadyThere() =>
        ExpireAndRead(ModeratorComment, comment =>
            comment.Should().Be(ModeratorComment + ResolutionComment.Separator + Reason));

    /// <summary>
    /// The other road into Expired: no moderator opened the request. It must leave
    /// the token deadline empty, because that emptiness is what the reply reads as
    /// "nobody looked at it" - a pass that filled the field would have the site
    /// telling the requester about an approval that never happened.
    /// </summary>
    [Fact]
    public async Task LeaveTheTokenDeadlineEmptyWhenNobodyReviewedTheRequest()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var username = $"unrev-{userId:N}"[..20];

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{username}@test.local",
            Salt = string.Empty,
            PasswordHash = string.Empty
        });
        dbContext.UsernameChangeRequests.Add(new DbUsernameChangeRequest
        {
            RequestId = requestId,
            UserId = userId,
            Reason = "Проверка истечения без рассмотрения",
            Status = UsernameChangeRequestStatus.Pending,
            CreatedUtc = Approved
        });
        await dbContext.SaveChangesAsync();

        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<IUsernameChangeRepository>();
            await repository.ExpireUnreviewedRequests(Now, Now, "Автоматически отклонено");

            var row = await dbContext.UsernameChangeRequests
                .AsNoTracking()
                .SingleAsync(r => r.RequestId == requestId);

            row.Status.Should().Be(UsernameChangeRequestStatus.Expired);
            row.ApprovalTokenExpiresUtc.Should().BeNull();
        }
        finally
        {
            await dbContext.UsernameChangeRequests.Where(r => r.RequestId == requestId).ExecuteDeleteAsync();
            await dbContext.Users.Where(u => u.UserId == userId).ExecuteDeleteAsync();
        }
    }

    private async Task ExpireAndRead(string? storedComment, Action<string?> assert)
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        // The column holds twenty characters, so the row is keyed by a short slice.
        var username = $"expiry-{userId:N}"[..20];
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{username}@test.local",
            Salt = string.Empty,
            PasswordHash = string.Empty
        });
        dbContext.UsernameChangeRequests.Add(new DbUsernameChangeRequest
        {
            RequestId = requestId,
            UserId = userId,
            Reason = "Проверка истечения одобрения",
            Status = UsernameChangeRequestStatus.Approved,
            CreatedUtc = Approved,
            ApprovalToken = Guid.NewGuid(),
            // Already past: the pass is meant to catch exactly this row.
            ApprovalTokenExpiresUtc = Approved.AddHours(48),
            ResolvedUtc = Approved,
            ResolverComment = storedComment
        });
        await dbContext.SaveChangesAsync();

        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<IUsernameChangeRepository>();
            // The pass is a single statement over every lapsed approval, so its
            // count says nothing about this row. Only the row itself is asserted.
            await repository.ExpireApprovalTokens(Now, Reason);

            var row = await dbContext.UsernameChangeRequests
                .AsNoTracking()
                .SingleAsync(r => r.RequestId == requestId);

            row.Status.Should().Be(UsernameChangeRequestStatus.Expired);
            // The token deadline stays on the row: it is what separates a lapsed
            // approval from a request no moderator ever opened, and the reply the
            // requester reads is built from that difference.
            row.ApprovalTokenExpiresUtc.Should().NotBeNull();
            assert(row.ResolverComment);
        }
        finally
        {
            await dbContext.UsernameChangeRequests.Where(r => r.RequestId == requestId).ExecuteDeleteAsync();
            await dbContext.Users.Where(u => u.UserId == userId).ExecuteDeleteAsync();
        }
    }
}
