using System;
using DM.Domain.Core.Tokens;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.Search;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbPendingRegistration = DM.Infrastructure.Persistence.Entities.Account.PendingRegistration;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using IProfileRepository = DM.Domain.Personal.Features.Profiles.IUserRepository;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Lookups that resolve one login or one address compare for equality, and stay
/// case-insensitive while doing it.
/// </summary>
/// <remarks>
/// Runs against the container Postgres because the whole question is which
/// operator the provider emits: ILIKE reads "_" and "%" in the value it is given
/// as wildcards, "=" over lower() does not, and no in-memory provider reproduces
/// the difference. Neither character is exotic here: the login policy lists "_"
/// among the allowed ones and it is legal in the local part of an address.
///
/// Every probe below is a login or an address that no row equals and exactly one
/// row matches as a pattern, so a lookup that answers "found" has stopped
/// comparing for equality. Each test also asks the same lookup for the row it
/// really owns, in upper case: case-insensitivity is what the pattern match was
/// paying for, and it has to survive.
/// </remarks>
public class IdentityLookupShould : IntegrationTestBase
{
    public IdentityLookupShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Six readers of Users answer "who owns this address". A pattern match tells
    /// a stranger their own address is taken and hands account operations a row
    /// that belongs to somebody else.
    /// </summary>
    [Fact]
    public async Task NotResolveAnAddressThatOnlyMatchesAnotherAddressAsAPattern()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var emailLookup = scope.ServiceProvider.GetRequiredService<IEmailLookupRepository>();
        var emailChange = scope.ServiceProvider.GetRequiredService<IEmailChangeRepository>();
        var registration = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var activation = scope.ServiceProvider.GetRequiredService<IActivationRepository>();
        var profiles = scope.ServiceProvider.GetRequiredService<IProfileRepository>();

        var (_, owner, probe) = await AddDecoyUserAsync(dbContext, "addr");
        var shouted = Address(owner).ToUpperInvariant();
        var wildcard = Address(probe);

        var found = await emailLookup.GetUserByEmail(shouted);
        found.Should().NotBeNull("the address is the owner's, spelled loudly");
        found!.Username.Should().Be(owner);
        (await emailLookup.EmailExists(shouted)).Should().BeTrue();
        (await emailChange.IsEmailFree(shouted, CancellationToken.None)).Should().BeFalse();
        (await registration.EmailFreeForNewRegistration(shouted, CancellationToken.None))
            .Should().BeFalse();
        (await activation.FindUserByEmail(shouted)).Should().NotBeNull();
        (await profiles.GetUserDetailsByEmail(shouted)).Should().NotBeNull();

        (await emailLookup.GetUserByEmail(wildcard)).Should().BeNull(
            "nobody owns that address, another one merely matches it as a pattern");
        (await emailLookup.EmailExists(wildcard)).Should().BeFalse();
        (await emailChange.IsEmailFree(wildcard, CancellationToken.None)).Should().BeTrue();
        (await registration.EmailFreeForNewRegistration(wildcard, CancellationToken.None))
            .Should().BeTrue();
        (await activation.FindUserByEmail(wildcard)).Should().BeNull();
        (await profiles.GetUserDetailsByEmail(wildcard)).Should().BeNull();
    }

    /// <summary>
    /// The same question against PendingRegistrations, which decides whether a
    /// registration is new and which row recovery resends a letter for.
    /// </summary>
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
        (await registration.PendingExists(shouted, CancellationToken.None)).Should().BeTrue();
        (await registration.FindPendingByEmail(shouted, CancellationToken.None))
            .Should().NotBeNull();

        (await registration.PendingExists(wildcard, CancellationToken.None)).Should().BeFalse();
        (await registration.FindPendingByEmail(wildcard, CancellationToken.None))
            .Should().BeNull();
    }

    /// <summary>
    /// ReplacePending overwrites the token and the password hash of the row it
    /// finds and leaves that row's address alone, while the confirmation letter
    /// goes to the address the caller typed. Matched as a pattern, it hands the
    /// caller a link that activates somebody else's registration under a password
    /// the caller chose.
    /// </summary>
    [Fact]
    public async Task ReplaceOnlyThePendingRegistrationWithTheSameAddress()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var registration = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();

        var ownerAddress = Address(Login("repl"));
        var callerAddress = Address(Probe("repl"));
        var ownerToken = await AddDecoyPendingAsync(dbContext, ownerAddress);

        await registration.ReplacePending(new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            SecretHash = ConfirmationSecret.Hash(Guid.NewGuid()),
            Email = callerAddress,
            PasswordHash = "caller-hash",
            Salt = "caller-salt",
            CreatedUtc = DateTimeOffset.UtcNow,
            TokenCreatedUtc = DateTimeOffset.UtcNow,
            AcceptedRules = true
        });

        var rows = await dbContext.PendingRegistrations
            .Where(p => p.Email == ownerAddress || p.Email == callerAddress)
            .ToListAsync();

        rows.Should().HaveCount(2,
            "the caller's address had no registration of its own, so it gets one");
        rows.Single(p => p.Email == ownerAddress).SecretHash.Should()
            .Equal(ConfirmationSecret.Hash(ownerToken),
                "the other registration keeps the activation secret it was issued");
    }

    /// <summary>
    /// FindUser resolves the person a direct chat is opened with, and the caller's
    /// next act is to write a private message into that chat.
    /// </summary>
    [Fact]
    public async Task NotOpenADirectChatWithSomebodyWhoseLoginOnlyMatchesAsAPattern()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var chats = scope.ServiceProvider.GetRequiredService<IChatRepository>();

        var (ownerId, owner, probe) = await AddDecoyUserAsync(dbContext, "chat");

        (await chats.FindUser(owner.ToUpperInvariant())).Should().Be(ownerId,
            "a login is matched regardless of case");
        (await chats.FindUser(probe)).Should().NotHaveValue(
            "no account carries that login, one merely matches it as a pattern");
        (await chats.FindUser("%")).Should().NotHaveValue(
            "no account is named after the single character %");
    }

    /// <summary>
    /// The from: operator names one author. All three branches of the search union
    /// carry the same filter, so the wildcard has to come back empty from each.
    /// </summary>
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

    /// <returns>The activation token the seeded registration was issued.</returns>
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
