using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbToken = DM.Infrastructure.Persistence.Entities.Account.Token;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The value a confirmation letter carries is not in the database.
/// </summary>
/// <remarks>
/// The value a letter carries is a credential, so the row keeps its hash. Against the container Postgres: the row's
/// content and a lookup by hash are asserted.
/// </remarks>
public class ConfirmationTokenStorageShould : IntegrationTestBase
{
    public ConfirmationTokenStorageShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task KeepNoTraceOfTheSecretItMailed()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<IPasswordResetRepository>();
        var factory = scope.ServiceProvider.GetRequiredService<ITokenFactory>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = await AddUserAsync(dbContext);
        var token = factory.Create(userId, TokenType.PasswordChange);

        await reset.ReplacePasswordResetToken(userId, token);

        var stored = await dbContext.Tokens.AsNoTracking()
            .Where(t => t.UserId == userId && t.Type == TokenType.PasswordChange)
            .SingleAsync();

        // The whole property, stated the way an attacker would use it: nothing in
        // the row equals the value that went out by mail.
        stored.TokenId.Should().NotBe(token.Secret,
            "the row identifier and the mailed value are different values on purpose");
        stored.SecretHash.Should().Equal(ConfirmationSecret.Hash(token.Secret));

        var rowsMatchingTheSecret = await dbContext.Tokens.AsNoTracking()
            .CountAsync(t => t.TokenId == token.Secret);
        rowsMatchingTheSecret.Should().Be(0,
            "a reader of the table cannot look a mailed secret up as an identifier");
    }

    [Fact]
    public async Task AnswerTheSecretAndNothingElse()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<IPasswordResetRepository>();
        var change = scope.ServiceProvider.GetRequiredService<IPasswordChangeRepository>();
        var factory = scope.ServiceProvider.GetRequiredService<ITokenFactory>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = await AddUserAsync(dbContext);
        var token = factory.Create(userId, TokenType.PasswordChange);
        await reset.ReplacePasswordResetToken(userId, token);

        var since = DateTimeOffset.UtcNow.AddHours(-1);

        (await change.TokenValid(token.Secret, since)).Should().BeTrue(
            "the secret from the letter is what redeems the reset");
        (await change.TokenValid(token.TokenId, since)).Should().BeFalse(
            "the row identifier is not a credential and must not open anything");
        (await change.TokenValid(Guid.NewGuid(), since)).Should().BeFalse();

        (await change.FindUser(token.Secret, since))!.UserId.Should().Be(userId);
    }

    /// <summary>
    /// An expired secret is refused by the lookup itself.
    /// </summary>
    /// <remarks>The type, the removal flag and the age are checked in the lookup, for every caller.</remarks>
    [Fact]
    public async Task RefuseASecretOlderThanTheWindow()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var change = scope.ServiceProvider.GetRequiredService<IPasswordChangeRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = await AddUserAsync(dbContext);
        var secret = Guid.NewGuid();
        dbContext.Tokens.Add(new DbToken
        {
            TokenId = Guid.NewGuid(),
            SecretHash = ConfirmationSecret.Hash(secret),
            UserId = userId,
            Type = TokenType.PasswordChange,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
        });
        await dbContext.SaveChangesAsync();

        var since = DateTimeOffset.UtcNow.AddHours(-1);
        (await change.TokenValid(secret, since)).Should().BeFalse();
        (await change.FindUser(secret, since)).Should().BeNull();

        // Still findable without a window: that is how the form tells "expired"
        // apart from "no such link".
        (await change.FindUser(secret, DateTimeOffset.MinValue)).Should().NotBeNull();
    }

    /// <summary>
    /// A confirmed change of address spends the link it came in through.
    /// </summary>
    /// <remarks>The second lookup runs against the row the first one left behind.</remarks>
    [Fact]
    public async Task SpendTheLinkTheSecretCameFrom()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var confirmation = scope.ServiceProvider
            .GetRequiredService<IEmailChangeConfirmationRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = await AddUserAsync(dbContext);
        var secret = Guid.NewGuid();
        dbContext.Tokens.Add(new DbToken
        {
            TokenId = Guid.NewGuid(),
            SecretHash = ConfirmationSecret.Hash(secret),
            UserId = userId,
            Type = TokenType.EmailChange,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var since = DateTimeOffset.UtcNow.AddHours(-1);
        (await confirmation.FindEmailChangeTokenOwner(secret, since)).Should().Be(userId);

        await confirmation.MarkTokenUsed(secret);

        (await confirmation.FindEmailChangeTokenOwner(secret, since)).Should().BeNull(
            "the same link followed a second time has nothing left to redeem");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Tokens.IgnoreQueryFilters().SingleAsync(t => t.UserId == userId))
            .IsRemoved.Should().BeTrue("and the row says so, whoever reads it next");
    }

    /// <summary>
    /// The owner's new password spends a change of address somebody else asked for.
    /// </summary>
    [Fact]
    public async Task DropThePendingAddressAndItsLinkWithThePassword()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordChangeRepository>();
        var confirmation = scope.ServiceProvider
            .GetRequiredService<IEmailChangeConfirmationRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = await AddUserAsync(dbContext);
        var user = await dbContext.Users.SingleAsync(u => u.UserId == userId);
        user.PendingEmail = $"moved-{userId:N}@example.com";
        var secret = Guid.NewGuid();
        dbContext.Tokens.Add(new DbToken
        {
            TokenId = Guid.NewGuid(),
            SecretHash = ConfirmationSecret.Hash(secret),
            UserId = userId,
            Type = TokenType.EmailChange,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await passwords.UpdatePassword(userId, "hash2", "salt2", secretToInvalidate: null);

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Users.SingleAsync(u => u.UserId == userId)).PendingEmail
            .Should().BeNull("the request the owner did not make goes with the old password");
        (await confirmation.FindEmailChangeTokenOwner(secret, DateTimeOffset.UtcNow.AddHours(-1)))
            .Should().BeNull("and the link in the other mailbox has nothing left to confirm");
    }

    /// <summary>
    /// Every token sent by mail keeps the hash of its secret, across the whole shared database.
    /// </summary>
    [Fact]
    public async Task KeepTheHashOfEveryMailedToken()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var mailed = new[] { TokenType.PasswordChange, TokenType.EmailChange };
        var rows = await dbContext.Tokens.AsNoTracking()
            .Where(t => mailed.Contains(t.Type))
            .Select(t => t.SecretHash)
            .ToListAsync();

        rows.Should().OnlyContain(hash => hash != null,
            "a token that is redeemed by knowing its value keeps the hash of that value");
    }

    private static Task<Guid> AddUserAsync(DmDbContext dbContext) => UserRows.AddAsync(dbContext, "tok");
}
