using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.UsernameChange;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Deactivation sets IsRemoved and nothing else, and the unique indexes over lower("Email")
/// and lower("Username") carry no predicate, so a deactivated account goes on holding its
/// address and its login. Every check that answers "may a new account take this" has to see
/// that row: asked through the global soft-delete filter it reports free what the database
/// refuses, and the refusal reaches the person as a 500 on the link that confirms the
/// registration, after the letter has been sent.
/// </summary>
public class AccountReservationShould : IntegrationTestBase
{
    public AccountReservationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Every caller at once, because they answer one question and used to answer it several
    /// different ways: the registration validator, the activation validator, the email change,
    /// the rename, and the two probes the forms call while the person is still typing.
    /// </summary>
    [Fact]
    public async Task RefuseTheAddressAndTheLoginOfADeactivatedAccount()
    {
        var account = await SeedDeactivatedAccountAsync();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var registration = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var emailChange = scope.ServiceProvider.GetRequiredService<IEmailChangeRepository>();
        var usernameChange = scope.ServiceProvider.GetRequiredService<IUsernameChangeRepository>();
        var availability = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();

        // Upper case on purpose: the indexes compare lower(), so a case variant is the same
        // reservation and has to come back with the same answer.
        (await registration.EmailFreeForNewRegistration(account.Email.ToUpperInvariant(), default))
            .Should().BeFalse("registration must not send a letter to an address the insert will refuse");
        (await registration.UsernameFree(account.Username.ToUpperInvariant(), default))
            .Should().BeFalse("a deactivated login stays reserved, exactly like an abandoned one");
        (await emailChange.IsEmailFree(account.Email, default))
            .Should().BeFalse("moving an address onto a deactivated account's address is the same collision");
        (await usernameChange.IsUsernameAvailable(account.Username))
            .Should().BeFalse("renaming into a deactivated account's login is the same collision");
        (await availability.CheckEmailAvailability(account.Email)).IsAvailable
            .Should().BeFalse("the probe behind the registration form answers the same question");
        (await availability.CheckUsernameAvailability(account.Username)).IsAvailable
            .Should().BeFalse("the probe behind the activation form answers the same question");
    }

    /// <summary>
    /// The other half of the rule, and the half that decides it: uniqueness covers the whole
    /// table. Freeing a deactivated address would mean a predicate on these indexes, and two
    /// rows would then hold one address the moment either of them came back.
    /// </summary>
    [Fact]
    public async Task LeaveTheDatabaseRefusingASecondAccountOnTheSameAddress()
    {
        var account = await SeedDeactivatedAccountAsync();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var twinId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = twinId,
            Username = $"t{twinId:N}"[..20],
            // The same address in another case: exactly what lower("Email") covers.
            Email = account.Email.ToUpperInvariant(),
            PasswordHash = "hash",
            Salt = "salt",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastActivityUtc = DateTimeOffset.UtcNow,
        });

        Func<Task> insert = () => dbContext.SaveChangesAsync();

        var refusal = await insert.Should().ThrowAsync<DbUpdateException>(
            "the unique index over lower(Email) carries no IsRemoved predicate, which is what " +
            "makes the reservation outlive the account");
        refusal.And.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505", "that is unique_violation");
    }

    /// <summary>
    /// An account of its own per run: the fixture's database is shared, and a reservation
    /// somebody else seeded would make the assertions depend on them. Deactivated through the
    /// repository the site uses, so the test cannot drift from what deactivation does.
    /// </summary>
    private async Task<(Guid UserId, string Username, string Email)> SeedDeactivatedAccountAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id fills it out.
        var username = $"d{userId:N}"[..20];
        var email = $"{userId:N}@example.com";
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = email,
            PasswordHash = "hash",
            Salt = "salt",
            CreatedUtc = DateTimeOffset.UtcNow,
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IDeactivationRepository>().DeactivateUser(userId);
        return (userId, username, email);
    }
}
