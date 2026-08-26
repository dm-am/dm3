using System;
using System.Linq;
using System.Threading.Tasks;
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
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The value a confirmation letter carries is not in the database.
/// </summary>
/// <remarks>
/// A password reset, an activation and an email change are redeemed by knowing a
/// value, which makes that value a credential and not an identifier. It used to be
/// stored as itself: Tokens.TokenId held exactly what the letter carried, so a
/// single read of that table — a dump, a backup, a copy restored for debugging, an
/// open console — handed over every account with an unspent token, without a
/// password and without access to the mailbox.
///
/// Runs against the container Postgres, because what is asserted is the content of
/// a row and the behaviour of a lookup by hash, not the shape of a call.
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
    /// <remarks>
    /// The type, the removal flag and the age used to be checked by whoever
    /// happened to call first, and one path — reading token info for the form —
    /// called the lookup with no check at all.
    /// </remarks>
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
    /// An invitation carries no secret, and that is the deliberate other half of
    /// the rule: it is redeemed by the addressee while signed in, so its identifier
    /// is an identifier.
    /// </summary>
    [Fact]
    public async Task LeaveInvitationsWithoutASecret()
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

    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is truncated
            // rather than used whole.
            Username = $"tok{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }
}
