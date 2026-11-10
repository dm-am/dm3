using System.Security.Cryptography;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUsernameChangeRequest = DM.Infrastructure.Persistence.Entities.Account.UsernameChangeRequest;
using UsernameChangeRequest = DM.Domain.Account.Features.UsernameChange.UsernameChangeRequest;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using PendingRegistration = DM.Domain.Account.Features.Registration.PendingRegistration;

namespace DM.Web.API.IntegrationTests.Repositories;

// Against the container Postgres: every assertion is about what the database decided.
public class AccountWriteRacesShould : IntegrationTestBase
{
    private const int Parallelism = 4;

    public AccountWriteRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // Submissions arriving together all find nothing; the unique index over the address decides.
    [Fact]
    public async Task TakeTheRegistrationFormTwiceWithoutFailing()
    {
        var email = $"race{Guid.NewGuid():N}@example.com";

        var writes = Enumerable.Range(0, Parallelism).Select(_ => AddPending(email));
        await Task.WhenAll(writes);

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.PendingRegistrations.CountAsync(p => p.Email == email))
            .Should().Be(1, "the address holds one pending registration whoever wrote it last");
    }

    // A resend keeps the moment the registration started, which is what frees an abandoned address.
    [Fact]
    public async Task KeepTheStartOfARegistrationWhenItIsRepeated()
    {
        var email = $"race{Guid.NewGuid():N}@example.com";
        var started = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeSeconds());

        await AddPending(email, started);
        await AddPending(email, started.AddDays(3));

        await using var db = DatabaseFixture.CreateDbContext();
        var row = await db.PendingRegistrations.SingleAsync(p => p.Email == email);
        row.CreatedUtc.Should().Be(started,
            "refreshed on every letter, an address could be held forever by pressing a button");
        row.TokenCreatedUtc.Should().Be(started.AddDays(3), "the letter is the new one");
    }

    [Fact]
    public async Task ReplaceAnUnconfirmedSecretAndRefuseOneThatIsOn()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var userId = await SeedUser(scope);
        var first = Moment();
        var second = first.AddMinutes(1);

        (await repository.IssueSecret(userId, "envelope:first", first)).Should().BeTrue();
        (await repository.IssueSecret(userId, "envelope:second", second))
            .Should().BeTrue("an abandoned QR may have been read over a shoulder");

        var replaced = await repository.Find(userId);
        replaced!.Secret.Should().Be("envelope:second");
        replaced.CreatedUtc.Should().Be(second);

        // A code accepted on the way to a confirmation that never happened: the
        // owner typed the six digits, the confirmation was refused, and the row is
        // left carrying the stamp of a secret that is about to be replaced.
        (await repository.TryAcceptStep(userId, 42, second.AddSeconds(30))).Should().BeTrue();

        var third = second.AddMinutes(2);
        (await repository.IssueSecret(userId, "envelope:third", third)).Should().BeTrue();

        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var reissued = await db.Set<UserTwoFactor>().SingleAsync(f => f.UserId == userId);
            reissued.LastVerifiedUtc.Should().BeNull(
                "a setup begun again begins on nothing, the way it did when the previous " +
                "row was deleted instead of replaced");
            reissued.LastAcceptedStep.Should().Be(0, "and the replay guard starts over with it");
        }

        (await repository.Confirm(userId, third, third.AddMinutes(1), Hashes(3))).Should().BeTrue();

        (await repository.IssueSecret(userId, "envelope:fourth", third.AddMinutes(3)))
            .Should().BeFalse("switching on what is already on would mean two live secrets");
        (await repository.Find(userId))!.Secret.Should().Be("envelope:third",
            "the refused setup left the confirmed secret exactly as it was");
    }

    /// <remarks>
    /// Together, not in sequence: the gap is what one transaction over the account row closes. The surviving letter
    /// is found first and the waiting address read against it.
    /// </remarks>
    [Fact]
    public async Task LeaveOneLiveLetterWhenChangeRequestsArriveTogether()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var userId = await SeedUser(scope);

        var asked = await Task.WhenAll(
            Enumerable.Range(0, Parallelism).Select(_ => RequestChange(userId)));

        await using var db = DatabaseFixture.CreateDbContext();
        var live = await db.Tokens.IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.Type == TokenType.EmailChange && !t.IsRemoved)
            .ToListAsync();
        live.Should().HaveCount(1,
            "only the letter of the request that wrote last may still be followed");

        var survivor = asked.Single(request => request.Hash.SequenceEqual(live[0].SecretHash!));
        (await db.Users.SingleAsync(u => u.UserId == userId)).PendingEmail.Should().Be(survivor.Email,
            "and the address waiting is the one that letter names");
    }

    private async Task<(string Email, byte[] Hash)> RequestChange(Guid userId)
    {
        // One scope per write: a DbContext is scoped and must not be shared
        // between calls running at the same time.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEmailChangeRepository>();
        var factory = scope.ServiceProvider.GetRequiredService<ITokenFactory>();

        var email = $"race{Guid.NewGuid():N}@example.com";
        var token = factory.Create(userId, TokenType.EmailChange);
        await repository.RequestChange(userId, email, token);
        return (email, token.SecretHash!);
    }

    /// <summary>
    /// A confirmation checked against a secret that has since been replaced
    /// switches nothing on.
    /// </summary>
    /// <remarks>A setup on another device overwrites the row in place, so the stamp checks the secret, not only "still off".</remarks>
    [Fact]
    public async Task SwitchNothingOnForASecretThatWasReplaced()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var userId = await SeedUser(scope);
        var first = Moment();
        var second = first.AddMinutes(1);

        await repository.IssueSecret(userId, "envelope:first", first);
        await repository.IssueSecret(userId, "envelope:second", second);

        (await repository.Confirm(userId, first, second.AddMinutes(1), Hashes(3)))
            .Should().BeFalse("the secret this confirmation was checked against is gone");

        var state = await repository.Find(userId);
        state!.ConfirmedUtc.Should().BeNull();
        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == userId))
            .Should().Be(0, "the set belongs to a confirmation that switched the factor on");
    }

    // The sweep removes listed accounts one at a time, and the call-off lands between.
    [Fact]
    public async Task LeaveTheFactorWhoseRemovalWasCalledOff()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var now = Moment();
        var scheduled = await SeedConfirmedFactor(scope, now, dueUtc: now.AddMinutes(-1));
        var cancelled = await SeedConfirmedFactor(scope, now, dueUtc: null);

        (await repository.RemoveIfDue(scheduled, now)).Should().BeTrue();
        (await repository.RemoveIfDue(cancelled, now))
            .Should().BeFalse("no removal is pending for this account any more");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Set<UserTwoFactor>().CountAsync(f => f.UserId == scheduled)).Should().Be(0);
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == scheduled))
            .Should().Be(0, "the codes go with the factor");
        (await db.Set<UserTwoFactor>().CountAsync(f => f.UserId == cancelled)).Should().Be(1);
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == cancelled))
            .Should().Be(3, "an account that kept its factor keeps the way past it");
    }

    // A mailed removal can come due between the checks and the write, so the write looks at what it
    // changes.
    [Fact]
    public async Task IssueNoRecoveryCodesForAFactorThatIsGone()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var now = Moment();
        var userId = await SeedConfirmedFactor(scope, now, dueUtc: now.AddMinutes(-1));

        (await repository.RemoveIfDue(userId, now)).Should().BeTrue();

        (await repository.ReplaceRecoveryCodes(userId, Hashes(10), now))
            .Should().BeFalse("there is no factor left for a set to belong to");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == userId))
            .Should().Be(0, "a set stored behind no factor is accepted by the next one");
    }

    // A row in setup is not a factor, and its secret is replaced in place by the next setup.
    [Fact]
    public async Task IssueNoRecoveryCodesForASetupThatWasNeverConfirmed()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITwoFactorRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var now = Moment();
        var userId = await SeedUser(scope);
        dbContext.Set<UserTwoFactor>().Add(new UserTwoFactor
        {
            UserId = userId,
            Secret = "envelope",
            CreatedUtc = now.AddMinutes(-1),
            ConfirmedUtc = null,
            LastAcceptedStep = 0
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        (await repository.ReplaceRecoveryCodes(userId, Hashes(10), now))
            .Should().BeFalse("the account has no factor switched on for a set to belong to");

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == userId))
            .Should().Be(0);
        (await db.Set<UserTwoFactor>()
                .Where(f => f.UserId == userId)
                .Select(f => f.RecoveryCodesIssuedUtc)
                .SingleAsync())
            .Should().BeNull("nothing was issued, so nothing is dated");
    }

    /// <summary>
    /// A rename the storage will not take comes back as a duplicate rather than
    /// as a failure of the storage engine.
    /// </summary>
    /// <remarks>lower("Username") over the account and IX_UsernameHistories_OldUsername over the history both stand over the write.</remarks>
    [Fact]
    public async Task RefuseARenameTheHistoryAlreadyHolds()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUsernameChangeRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = await SeedUser(scope);
        var current = await dbContext.Users
            .Where(u => u.UserId == userId).Select(u => u.Username).SingleAsync();
        var now = Moment();
        var requestId = Guid.NewGuid();

        dbContext.Set<DbUsernameChangeRequest>().Add(new DbUsernameChangeRequest
        {
            RequestId = requestId,
            UserId = userId,
            Reason = "the name no longer fits",
            Status = UsernameChangeRequestStatus.Approved,
            CreatedUtc = now,
            ApprovalToken = Guid.NewGuid(),
            ApprovalTokenExpiresUtc = now.AddDays(1)
        });
        // The name the account is about to leave behind is already in the
        // history, which is what taking a previous name back leaves behind.
        dbContext.Set<DbUsernameHistory>().Add(new DbUsernameHistory
        {
            UsernameHistoryId = Guid.NewGuid(),
            UserId = userId,
            OldUsername = current,
            NewUsername = $"rb{Guid.NewGuid():N}"[..20],
            ChangedUtc = now.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var chosen = $"rn{Guid.NewGuid():N}"[..20];
        var act = () => repository.ApplyRename(
            new UsernameChangeRequest
            {
                RequestId = requestId,
                UserId = userId,
                RequestedUsername = chosen,
                Reason = "the name no longer fits",
                Status = UsernameChangeRequestStatus.Completed,
                CreatedUtc = now,
                ResolvedUtc = now
            },
            new CreateUsernameHistory
            {
                UsernameHistoryId = Guid.NewGuid(),
                UserId = userId,
                OldUsername = current,
                NewUsername = chosen,
                ChangedUtc = now
            });

        await act.Should().ThrowAsync<DuplicateEntityException>();

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Users.SingleAsync(u => u.UserId == userId)).Username.Should().Be(current,
            "a refused write leaves the account under the name it had");
    }

    // A reset link outliving the change could set the password again.
    [Fact]
    public async Task LeaveNoLiveResetLinkAfterThePasswordChanges()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<IPasswordResetRepository>();
        var change = scope.ServiceProvider.GetRequiredService<IPasswordChangeRepository>();
        var factory = scope.ServiceProvider.GetRequiredService<ITokenFactory>();
        var userId = await SeedUser(scope);

        var used = factory.Create(userId, TokenType.PasswordChange);
        await reset.ReplacePasswordResetToken(userId, used);
        var alsoAskedFor = factory.Create(userId, TokenType.PasswordChange);
        await reset.ReplacePasswordResetToken(userId, alsoAskedFor);

        await change.UpdatePassword(userId, "newhash", "newsalt", used.Secret);

        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Tokens.IgnoreQueryFilters().CountAsync(t =>
                t.UserId == userId && t.Type == TokenType.PasswordChange && !t.IsRemoved))
            .Should().Be(0, "every live link of the account goes with the password it could replace");
    }

    // Removed between the lookup and the write: the caller must learn the password did not change.
    [Fact]
    public async Task WriteNothingForAnAccountRemovedBeforeThePasswordChange()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var reset = scope.ServiceProvider.GetRequiredService<IPasswordResetRepository>();
        var change = scope.ServiceProvider.GetRequiredService<IPasswordChangeRepository>();
        var factory = scope.ServiceProvider.GetRequiredService<ITokenFactory>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = await SeedUser(scope);
        var link = factory.Create(userId, TokenType.PasswordChange);
        await reset.ReplacePasswordResetToken(userId, link);
        await dbContext.Users
            .Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsRemoved, true));

        var updated = await change.UpdatePassword(userId, "newhash", "newsalt", link.Secret);

        updated.Should().BeFalse();
        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Users.IgnoreQueryFilters().SingleAsync(u => u.UserId == userId)).PasswordHash
            .Should().Be("hash", "a removed account keeps the password it had");
        (await db.Tokens.IgnoreQueryFilters().CountAsync(t =>
                t.UserId == userId && t.Type == TokenType.PasswordChange && !t.IsRemoved))
            .Should().Be(1, "nothing of a write that found no account is kept");
    }

    // The unique index over lower("Email") decides; this is how its refusal reaches the caller.
    [Fact]
    public async Task RefuseAPendingAddressSomebodyElseTook()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEmailChangeRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var contested = $"contested{Guid.NewGuid():N}@example.com";
        var waiting = await SeedUser(scope);
        await dbContext.Users
            .Where(u => u.UserId == waiting)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PendingEmail, contested));
        await SeedUser(scope, contested);

        var act = () => repository.ApplyPendingEmail(waiting);

        await act.Should().ThrowAsync<DuplicateEntityException>();

        await using var db = DatabaseFixture.CreateDbContext();
        var unchanged = await db.Users.SingleAsync(u => u.UserId == waiting);
        unchanged.Email.Should().NotBe(contested, "the address stayed with the account that took it");
        unchanged.PendingEmail.Should().BeNull(
            "the request is over: an address that can never be confirmed would keep the " +
            "profile advertising a change nobody can complete");
    }

    // Whole seconds: the columns keep microseconds and are compared.
    private static DateTimeOffset Moment() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private static List<byte[]> Hashes(int count) =>
        Enumerable.Range(0, count).Select(_ => RandomNumberGenerator.GetBytes(32)).ToList();

    private async Task AddPending(string email, DateTimeOffset? startedUtc = null)
    {
        // One scope per write: a DbContext is scoped and must not be shared
        // between calls running at the same time.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var started = startedUtc ?? Moment();
        var secret = Guid.NewGuid();
        await repository.AddPending(new PendingRegistration
        {
            PendingRegistrationId = Guid.NewGuid(),
            Secret = secret,
            SecretHash = ConfirmationSecret.Hash(secret),
            Email = email,
            PasswordHash = "hash",
            Salt = "salt",
            CreatedUtc = started,
            TokenCreatedUtc = started,
            AcceptedRules = true
        });
    }

    private static async Task<Guid> SeedUser(IServiceScope scope, string? email = null)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            // Username is varchar(20) and uniquely indexed, so the id is truncated
            // rather than used whole.
            UserId = userId,
            Username = $"rc{userId:N}"[..20],
            Email = email ?? $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return userId;
    }

    private static async Task<Guid> SeedConfirmedFactor(
        IServiceScope scope, DateTimeOffset now, DateTimeOffset? dueUtc)
    {
        var userId = await SeedUser(scope);
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Set<UserTwoFactor>().Add(new UserTwoFactor
        {
            UserId = userId,
            Secret = "envelope",
            CreatedUtc = now.AddDays(-8),
            ConfirmedUtc = now.AddDays(-8),
            RecoveryCodesIssuedUtc = now.AddDays(-8),
            RemovalDueUtc = dueUtc,
            LastAcceptedStep = 0
        });
        dbContext.Set<UserTwoFactorRecoveryCode>().AddRange(Hashes(3).Select(hash =>
            new UserTwoFactorRecoveryCode
            {
                RecoveryCodeId = Guid.NewGuid(),
                UserId = userId,
                CodeHash = hash,
                IssuedUtc = now.AddDays(-8)
            }));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return userId;
    }
}
