using DM.Domain.Core.Tokens;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Domain.Messaging.Features.Search;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbPendingRegistration = DM.Infrastructure.Persistence.Entities.Account.PendingRegistration;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <remarks>
/// ILIKE reads "_" and "%" as wildcards and "=" over lower() does not; "_" is a legal login character. Every probe
/// equals no row and matches one as a pattern, and each test asks for the owned row in upper case too.
/// </remarks>
public class IdentityLookupShould : IntegrationTestBase
{
    public IdentityLookupShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task NotResolveAnAddressThatOnlyMatchesAnotherAddressAsAPattern()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var emailLookup = scope.ServiceProvider.GetRequiredService<IEmailLookupRepository>();
        var emailChange = scope.ServiceProvider.GetRequiredService<IEmailChangeRepository>();
        var registration = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var activation = scope.ServiceProvider.GetRequiredService<IActivationRepository>();

        var (_, owner, probe) = await AddDecoyUserAsync(dbContext, "addr");
        var shouted = Address(owner).ToUpperInvariant();
        var wildcard = Address(probe);

        var found = await emailLookup.GetUserByEmail(shouted);
        found.Should().NotBeNull("the address is the owner's, spelled loudly");
        found!.Username.Should().Be(owner);
        (await emailChange.IsEmailFree(shouted, CancellationToken.None)).Should().BeFalse();
        (await registration.EmailFreeForNewRegistration(shouted, CancellationToken.None))
            .Should().BeFalse();
        (await activation.FindUserByEmail(shouted)).Should().NotBeNull();

        (await emailLookup.GetUserByEmail(wildcard)).Should().BeNull(
            "nobody owns that address, another one merely matches it as a pattern");
        (await emailChange.IsEmailFree(wildcard, CancellationToken.None)).Should().BeTrue();
        (await registration.EmailFreeForNewRegistration(wildcard, CancellationToken.None))
            .Should().BeTrue();
        (await activation.FindUserByEmail(wildcard)).Should().BeNull();
    }

    [Fact]
    public async Task NotFindAPendingRegistrationThatOnlyMatchesAsAPattern()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var registration = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();

        var owned = Address(Login("pend"));
        var wildcard = Address(Probe("pend"));
        await AddDecoyPendingAsync(dbContext, owned);

        var shouted = owned.ToUpperInvariant();
        (await registration.FindPendingByEmail(shouted, CancellationToken.None))
            .Should().NotBeNull();

        (await registration.FindPendingByEmail(wildcard, CancellationToken.None))
            .Should().BeNull();
    }

    // The lookup by login resolves the person a direct chat is opened with, and the caller's next
    // act is to write a private message into that chat.
    [Fact]
    public async Task NotOpenADirectChatWithSomebodyWhoseLoginOnlyMatchesAsAPattern()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<IUserReadRepository>();

        var (ownerId, owner, probe) = await AddDecoyUserAsync(dbContext, "chat");

        (await users.FindUserIdAsync(owner.ToUpperInvariant())).Should().Be(ownerId,
            "a login is matched regardless of case");
        (await users.FindUserIdAsync(probe)).Should().NotHaveValue(
            "no account carries that login, one merely matches it as a pattern");
        (await users.FindUserIdAsync("%")).Should().NotHaveValue(
            "no account is named after the single character %");
    }

    // All three branches of the search union carry the same filter, so the wildcard has to come
    // back empty from each.
    [Fact]
    public async Task FilterSearchByTheAuthorLoginRatherThanByAPattern()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IMessageSearchRepository>();

        var byAuthor = await search.Search(TestConstants.TestUserId, new MessageSearchQuery
        {
            FromUsername = TestConstants.TestUserLogin.ToUpperInvariant(),
            Limit = CursorQuery.MaxLimit
        });

        // Without these three the emptiness below would also hold for a branch that
        // returns nothing at all, and would pin nothing about that branch.
        byAuthor.Data.Should().Contain(h => h.SourceType == "global");
        byAuthor.Data.Should().Contain(h => h.SourceType == "chat");
        byAuthor.Data.Should().Contain(h => h.SourceType == "game");

        var byWildcard = await search.Search(TestConstants.TestUserId, new MessageSearchQuery
        {
            FromUsername = "%",
            Limit = CursorQuery.MaxLimit
        });

        byWildcard.Data.Should().BeEmpty(
            "no author is named after the single character %");
    }

    // The owned login and the probe differ by exactly the one character a LIKE "_"
    // swallows. Kept per-test-unique so the rows this class adds cannot reach each
    // other, and short enough for the twenty-character login column.
    private static string Login(string slot) => $"lk{slot}xrd";

    private static string Probe(string slot) => $"lk{slot}_rd";

    private static string Address(string login) => $"{login}@example.com";

    private static async Task<(Guid Id, string Login, string Probe)> AddDecoyUserAsync(
        DmDbContext dbContext, string slot)
    {
        var userId = Guid.NewGuid();
        var login = Login(slot);
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = login,
            Email = Address(login),
            PasswordHash = "fakehash",
            Salt = "fakesalt",
            Role = UserRole.RegularUser,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastActivityUtc = DateTimeOffset.UtcNow,
            IsRemoved = false,
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
            Info = string.Empty
        });
        await dbContext.SaveChangesAsync();
        return (userId, login, Probe(slot));
    }

    private static async Task<Guid> AddDecoyPendingAsync(DmDbContext dbContext, string email)
    {
        var tokenId = Guid.NewGuid();
        dbContext.PendingRegistrations.Add(new DbPendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            SecretHash = ConfirmationSecret.Hash(tokenId),
            Email = email,
            PasswordHash = "fakehash",
            Salt = "fakesalt",
            CreatedUtc = DateTimeOffset.UtcNow,
            TokenCreatedUtc = DateTimeOffset.UtcNow,
            AcceptedRules = true
        });
        await dbContext.SaveChangesAsync();
        return tokenId;
    }
}
