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

/// <summary>
/// Two requests to one account arriving together, on the paths where the loser
/// used to be answered with a failure of the storage engine or with nothing at all.
/// </summary>
/// <remarks>
/// Against the container Postgres because every assertion here is about what the
/// database decided: a unique index refusing a second row, an upsert predicate
/// refusing an update, a delete that carried its own condition. A substitute
/// returns what it was told to and proves none of it.
/// </remarks>
public class AccountWriteRacesShould : IntegrationTestBase
{
    private const int Parallelism = 4;

    public AccountWriteRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // ── Registration ──

    /// <summary>
    /// The form submitted twice leaves one pending registration and no failure.
    /// </summary>
    /// <remarks>
    /// The service asks whether the address already has a pending row and writes
    /// according to the answer, and the answer stops being true the moment it is
    /// given: submissions arriving together all found nothing and all inserted,
    /// and the unique index over the address answered every loser with a 500 on
    /// the very first screen of the site.
    /// </remarks>
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

    /// <summary>
    /// A resend keeps the moment the registration started, which is what frees an
    /// abandoned address.
    /// </summary>
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

    // ── The second factor ──

    /// <summary>
    /// A setup started again replaces the secret; once the factor is on, nothing
    /// replaces anything.
    /// </summary>
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

    /// <summary>
    /// Change requests arriving together leave one live letter, and it names the
    /// address that letter was asked for.
    /// </summary>
    /// <remarks>
    /// Retiring the account's old links and writing the new pending address used to
    /// be two calls, and a request from another tab landed between them: both
    /// retired what they found and both wrote a letter, so two links were live at
    /// once and the older of them confirmed an address the owner had already
    /// replaced.
    ///
    /// Together and not one after the other, because that gap is the whole of what
    /// is being tested: run in sequence, the second request finds the first one
    /// committed and the arrangement this test exists to hold - one transaction
    /// over the account row - can be taken out with every assertion still passing.
    /// Which of them wins is not fixed, so the surviving letter is found first and
    /// the waiting address is read against it.
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

    /// <summary>
    /// One change request, in a scope of its own, and the two values it has to be
    /// recognised by afterwards.
    /// </summary>
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
    /// <remarks>
    /// The window is between reading the state and stamping it: a setup begun on
    /// another device overwrites the row in place, and a stamp asking only
    /// "still off?" would switch the account on to a secret nobody here has seen,
    /// and hand out the recovery set that goes with it.
    /// </remarks>
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

    /// <summary>
    /// A removal called off before the pass reaches it takes neither the factor
    /// nor the codes.
    /// </summary>
    /// <remarks>
    /// The sweep lists the accounts whose waiting period is over and removes them
    /// one at a time, and calling the removal off lands in the gap between the
    /// two. Told by the listing alone, the pass took the factor - and the recovery
    /// codes with it - off an owner who had just kept it.
    /// </remarks>
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

    /// <summary>
    /// A reissue asked for a factor that is gone hands out nothing and stores
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The state is read, the password and the code are checked, and then the set
    /// is written - and a mailed removal coming due in that gap needs no code of
    /// its own. Written without looking at what it changed, this answered the
    /// owner with ten recovery codes for a factor that was off and left the rows
    /// behind no factor at all, where the next one the account switches on would
    /// accept them.
    /// </remarks>
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

    /// <summary>
    /// A reissue asked for a setup nobody ever confirmed hands out nothing.
    /// </summary>
    /// <remarks>
    /// The row exists while a setup is in flight, and it is not a factor: the
    /// account is not protected by it, the QR may be sitting on a screen nobody
    /// owns, and the secret is replaced in place by the next setup that starts.
    /// A set written against it would be a way past a factor the account has not
    /// switched on, and it survives into whatever secret takes that row next.
    /// </remarks>
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

    // ── The name ──

    /// <summary>
    /// A rename the storage will not take comes back as a duplicate rather than
    /// as a failure of the storage engine.
    /// </summary>
    /// <remarks>
    /// Two unique structures stand over that write: lower("Username") over the
    /// account, which a name taken between the service's read and the write runs
    /// into, and IX_UsernameHistories_OldUsername over the history, which needs
    /// no race at all - an account that once took its previous name back is
    /// about to leave behind a name the history already holds. Untranslated,
    /// both reached the reader as a 500 on the very form that exists to let them
    /// choose another name.
    /// </remarks>
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

    // ── Password and address ──

    /// <summary>
    /// A password change leaves no live reset link behind it, including one issued
    /// while the change was under way.
    /// </summary>
    /// <remarks>
    /// Only the link the change came in through used to be spent. A second letter
    /// requested a moment earlier outlived the change, and whoever held it could
    /// set the password again over the one its owner had just chosen - which is
    /// the whole of what a reset link is for.
    /// </remarks>
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

    /// <summary>
    /// An address taken while the confirmation letter waited is refused as taken,
    /// not as a failure of the storage engine.
    /// </summary>
    /// <remarks>
    /// Nothing holds the address between the request and the click: the check that
    /// it is free is a read, and whoever writes it first owns it. The unique index
    /// over lower("Email") is what actually decides, and this is the shape its
    /// refusal reaches the caller in.
    /// </remarks>
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

    // ── Seeding ──

    /// <summary>Whole seconds: the columns keep microseconds and are compared.</summary>
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
