using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUsernameChangeRequest = DM.Infrastructure.Persistence.Entities.Account.UsernameChangeRequest;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// The account's own name change request is answered, in every state it can be in.
/// </summary>
/// <remarks>
/// Every reply of this feature is mapped from the domain entry, and the map did
/// not exist: `GET /v1/account/username-change` answered 500 to anyone who had
/// ever filed a request, filing one wrote the row and then failed on the reply,
/// and finishing the change renamed the account and failed on the reply too. The
/// one account that got an answer was the one that had never asked, because that
/// path returns no content and maps nothing - which is exactly the account every
/// other test of this area used.
///
/// So the assertion here is deliberately unglamorous: file nothing, ask, and read
/// the payload. What it guards is that a reply exists at all.
/// </remarks>
public class UsernameChangeStatusShould : IntegrationTestBase
{
    private const string Url = "/v1/account/username-change";

    private static readonly DateTimeOffset Filed = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    public UsernameChangeStatusShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// An account that never asked gets no content, and nothing is mapped on that
    /// path - the state every other test happened to use.
    /// </summary>
    [Fact]
    public async Task AnswerWithNoContentWhenNothingWasEverAsked()
    {
        var (user, _) = await Seed(null);
        try
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, Url, User(user)));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await Drop(user);
        }
    }

    /// <summary>
    /// Every state the flow reaches comes back as a payload rather than an error,
    /// and carries the status the row holds.
    /// </summary>
    [Theory]
    [InlineData(UsernameChangeRequestStatus.Pending)]
    [InlineData(UsernameChangeRequestStatus.Approved)]
    [InlineData(UsernameChangeRequestStatus.Rejected)]
    [InlineData(UsernameChangeRequestStatus.Completed)]
    [InlineData(UsernameChangeRequestStatus.Expired)]
    public async Task AnswerTheRequestInEveryStateItCanReach(UsernameChangeRequestStatus status)
    {
        var (user, _) = await Seed(status);
        try
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, Url, User(user)));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            payload.GetProperty("status").GetString().Should().Be(status.ToString());
            payload.GetProperty("reason").GetString().Should().NotBeNullOrEmpty();
        }
        finally
        {
            await Drop(user);
        }
    }

    /// <summary>
    /// Expiry has two roads into one status, and the payload separates them so the
    /// account is not told an approval happened when none did.
    /// </summary>
    [Theory]
    [InlineData(false, "Unreviewed")]
    [InlineData(true, "ApprovalLapsed")]
    public async Task SayWhichDeadlineExpiredTheRequest(bool wasApproved, string expected)
    {
        var (user, _) = await Seed(UsernameChangeRequestStatus.Expired, wasApproved);
        try
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, Url, User(user)));

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            payload.GetProperty("expiryReason").GetString().Should().Be(expected);
        }
        finally
        {
            await Drop(user);
        }
    }

    /// <summary>
    /// The reason belongs to expiry alone: a live approval carries the same token
    /// expiry and must not be labelled with it.
    /// </summary>
    [Fact]
    public async Task LeaveTheExpiryReasonUnsetOnALiveApproval()
    {
        var (user, _) = await Seed(UsernameChangeRequestStatus.Approved, wasApproved: true);
        try
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, Url, User(user)));

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            // The serializer drops nulls, so "unset" is a missing property here.
            var carried = payload.TryGetProperty("expiryReason", out var reason)
                          && reason.ValueKind != JsonValueKind.Null;
            carried.Should().BeFalse();
        }
        finally
        {
            await Drop(user);
        }
    }

    private static DM.Domain.Core.Dto.GeneralUser User(Guid userId) => new()
    {
        UserId = userId,
        Username = Name(userId),
        Role = UserRole.RegularUser
    };

    private static string Name(Guid userId) => $"ucs-{userId:N}"[..20];

    private async Task<(Guid User, Guid Request)> Seed(
        UsernameChangeRequestStatus? status, bool wasApproved = false)
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = Name(userId),
            Email = $"{Name(userId)}@test.local",
            Salt = string.Empty,
            PasswordHash = string.Empty
        });

        if (status.HasValue)
        {
            // The approval branch is the only writer of the token expiry, and the
            // expiry pass leaves it on the row: that is what separates the two
            // roads into Expired.
            var approved = wasApproved || status == UsernameChangeRequestStatus.Approved
                                       || status == UsernameChangeRequestStatus.Completed;
            dbContext.UsernameChangeRequests.Add(new DbUsernameChangeRequest
            {
                RequestId = requestId,
                UserId = userId,
                Reason = "Хочу имя без опечатки",
                Status = status.Value,
                CreatedUtc = Filed,
                ApprovalToken = approved ? Guid.NewGuid() : null,
                ApprovalTokenExpiresUtc = approved ? Filed.AddHours(48) : null,
                ResolvedUtc = status == UsernameChangeRequestStatus.Pending ? null : Filed
            });
        }

        await dbContext.SaveChangesAsync();
        return (userId, requestId);
    }

    private async Task Drop(Guid userId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.UsernameChangeRequests.Where(r => r.UserId == userId).ExecuteDeleteAsync();
        await dbContext.Users.Where(u => u.UserId == userId).ExecuteDeleteAsync();
    }
}
