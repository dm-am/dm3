using System.Security.Cryptography;
using DM.Domain.Account.Features.TwoFactor;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Switching the factor on and issuing its recovery set is one change or none.
/// </summary>
/// <remarks>
/// The design asks for one transaction here, and the reason is the answer rather
/// than the table: the recovery set exists in the reply to the confirmation and
/// nowhere else afterwards. A stamp that survived a failed set would leave the
/// owner switched on, tied to the single device holding the secret, with no code
/// to get past it and a week-long mailed removal as the whole of his way back.
///
/// Run against the real database because a rollback is not a property of the code
/// that asks for it. Written over two calls the failure looked identical from the
/// service - one write returned, the next threw - and only the rows say which of
/// the two arrangements was in place.
/// </remarks>
public class TwoFactorConfirmationShould : IntegrationTestBase
{
    public TwoFactorConfirmationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task StoreTheStampAndTheWholeSetTogether()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var userId = await SeedUnconfirmedFactor(scope);
        var confirmedUtc = DateTimeOffset.UtcNow;

        (await repository.Confirm(userId, confirmedUtc, Hashes(10)))
            .Should().BeTrue("the factor was off and this call is what switched it on");

        await using var db = DatabaseFixture.CreateDbContext();
        var state = await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == userId);
        state.ConfirmedUtc.Should().NotBeNull();
        state.RecoveryCodesIssuedUtc.Should().NotBeNull(
            "the set the owner is about to be shown is dated by the same write");
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == userId))
            .Should().Be(10);
    }

    /// <summary>
    /// A failure part-way through leaves the factor off, not on and empty-handed.
    /// </summary>
    /// <remarks>
    /// The set is poisoned with a row the column refuses, which is a failure
    /// arriving after the stamp has already been written inside the transaction -
    /// the exact ordering the two-call arrangement could not roll back.
    /// </remarks>
    [Fact]
    public async Task LeaveTheFactorOffWhenTheSetCannotBeStored()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var userId = await SeedUnconfirmedFactor(scope);

        var poisoned = Hashes(10);
        poisoned[4] = null!;

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.Confirm(userId, DateTimeOffset.UtcNow, poisoned));

        await using var db = DatabaseFixture.CreateDbContext();
        var state = await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == userId);
        state.ConfirmedUtc.Should().BeNull(
            "a factor switched on for an owner who never saw a recovery code is the one " +
            "outcome this operation exists to rule out");
        state.RecoveryCodesIssuedUtc.Should().BeNull();
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == userId))
            .Should().Be(0, "half a set is a set the owner cannot be told the size of");
    }

    [Fact]
    public async Task RefuseTheConfirmationThatLostTheRace()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var userId = await SeedUnconfirmedFactor(scope);

        (await repository.Confirm(userId, DateTimeOffset.UtcNow, Hashes(10))).Should().BeTrue();
        (await repository.Confirm(userId, DateTimeOffset.UtcNow, Hashes(10)))
            .Should().BeFalse("the stamp is a conditional update and only one caller writes it");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == userId))
            .Should().Be(10, "the loser issues nothing: the set belongs to the caller that won");
    }

    private static List<byte[]> Hashes(int count) =>
        Enumerable.Range(0, count).Select(_ => RandomNumberGenerator.GetBytes(32)).ToList();

    private static async Task<Guid> SeedUnconfirmedFactor(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = $"f{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow
        });
        dbContext.Set<UserTwoFactor>().Add(new UserTwoFactor
        {
            UserId = userId,
            Secret = "envelope",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastAcceptedStep = 0
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return userId;
    }
}
