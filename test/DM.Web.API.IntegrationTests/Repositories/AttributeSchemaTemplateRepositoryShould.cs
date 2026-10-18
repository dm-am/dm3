using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The catalogue of starting character sheets, against a live Postgres.
/// </summary>
/// <remarks>
/// Every rule here is one the database settles and nothing else can: the two
/// partial unique indexes exist only under PostgreSQL, and the conditional
/// writes that hide a template and move the default flag are predicates on a
/// row rather than decisions taken in memory.
/// </remarks>
public class AttributeSchemaTemplateRepositoryShould : IntegrationTestBase
{
    public AttributeSchemaTemplateRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The table is shared with every other test of the run, and the default
    /// flag is a fact about the whole table, so each test starts from a
    /// catalogue that has no default at all.
    /// </summary>
    private static async Task ClearTheDefaultFlag(DmDbContext dbContext) =>
        await dbContext.AttributeSchemaTemplates
            .Where(t => t.IsDefault)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsDefault, false));

    private static string UniqueTitle() => $"Шаблон {Guid.NewGuid():N}";

    private static CreateAttributeSchemaTemplate Creation(string title) => new()
    {
        Title = title,
        Specifications =
        [
            new CreateAttributeSpecification
            {
                Title = "Сила",
                Type = AttributeSpecificationType.Text,
                Order = 0
            }
        ]
    };

    /// <summary>
    /// The exemption from the global soft-delete filter is gone with the reason
    /// for it, so a hidden template is invisible to both reads without either of
    /// them spelling a predicate.
    /// </summary>
    [Fact]
    public async Task HideATemplateInsteadOfDroppingTheRow()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var created = await repository.Create(Creation(UniqueTitle()), author);

        (await repository.Delete(created.Id)).Should().Be(AttributeSchemaTemplateRemoval.Hidden);

        var row = await dbContext.AttributeSchemaTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateId == created.Id);

        row.Should().NotBeNull("the removal is soft, so the row outlives it");
        row!.IsRemoved.Should().BeTrue("the removal writes the flag the class declares");

        (await repository.GetTemplate(created.Id)).Should().BeNull(
            "a hidden template is invisible to the single read");
        (await repository.GetTemplates()).Should().NotContain(t => t.Id == created.Id,
            "a hidden template is invisible to the catalogue");
    }

    [Fact]
    public async Task AnswerNotFoundWhenTheSameTemplateIsHiddenTwice()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var created = await repository.Create(Creation(UniqueTitle()), author);

        await repository.Delete(created.Id);

        (await repository.Delete(created.Id)).Should().Be(AttributeSchemaTemplateRemoval.NotFound);
    }

    /// <summary>
    /// Two live templates cannot share a name, on either write that can take
    /// one.
    /// </summary>
    [Fact]
    public async Task RefuseATitleAnotherLiveTemplateCarries()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var taken = UniqueTitle();
        await repository.Create(Creation(taken), author);
        var other = await repository.Create(Creation(UniqueTitle()), author);

        var second = async () => await repository.Create(Creation(taken), author);
        await second.Should().ThrowAsync<DuplicateEntityException>(
            "two templates under one name are indistinguishable in the creation form");

        var rename = async () => await repository.Replace(new ReplaceAttributeSchemaTemplate
        {
            TemplateId = other.Id,
            Title = taken,
            Specifications = []
        });
        await rename.Should().ThrowAsync<DuplicateEntityException>(
            "the rule holds however the name is arrived at");
    }

    /// <summary>
    /// The uniqueness is over the live catalogue: a name freed by hiding its
    /// template can be used again.
    /// </summary>
    [Fact]
    public async Task LetTheTitleOfAHiddenTemplateBeUsedAgain()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var title = UniqueTitle();
        var first = await repository.Create(Creation(title), author);
        await repository.Delete(first.Id);

        var again = await repository.Create(Creation(title), author);

        again.Id.Should().NotBe(first.Id);
        again.Title.Should().Be(title);
    }

    /// <summary>
    /// A replacement writes every field, and the specifications come back with
    /// identifiers of the server's own: nothing addresses a specification of a
    /// template, so none of them is preserved.
    /// </summary>
    [Fact]
    public async Task HandOutFreshSpecificationIdentifiersOnEveryReplacement()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var created = await repository.Create(Creation(UniqueTitle()), author);
        var before = created.Specifications.Single().Id;

        var replaced = await repository.Replace(new ReplaceAttributeSchemaTemplate
        {
            TemplateId = created.Id,
            Title = UniqueTitle(),
            Specifications =
            [
                new CreateAttributeSpecification
                {
                    Title = "Ловкость",
                    Type = AttributeSpecificationType.Text,
                    Order = 0
                }
            ]
        });

        replaced.Should().NotBeNull();
        var after = replaced!.Specifications.Single();
        after.Title.Should().Be("Ловкость");
        after.Id.Should().NotBe(before);
        after.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnswerNothingWhenAReplacementFindsNoLiveTemplate()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var created = await repository.Create(Creation(UniqueTitle()), author);
        await repository.Delete(created.Id);

        var replaced = await repository.Replace(new ReplaceAttributeSchemaTemplate
        {
            TemplateId = created.Id,
            Title = UniqueTitle(),
            Specifications = []
        });

        replaced.Should().BeNull("a hidden template is nothing to write into");
    }

    /// <summary>
    /// The flag moves off the template that carried it and onto the named one,
    /// in one write nobody can observe half of.
    /// </summary>
    [Fact]
    public async Task MoveTheDefaultFlagOntoTheNamedTemplate()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(dbContext);
        var author = await SeedUser(dbContext);
        var first = await repository.Create(Creation(UniqueTitle()), author);
        var second = await repository.Create(Creation(UniqueTitle()), author);

        (await repository.MakeDefault(first.Id)).Should().BeTrue();
        (await repository.MakeDefault(second.Id)).Should().BeTrue();

        (await repository.GetTemplate(first.Id))!.IsDefault.Should().BeFalse(
            "the flag left the template that carried it");
        (await repository.GetTemplate(second.Id))!.IsDefault.Should().BeTrue();
        (await LiveDefaults(dbContext)).Should().HaveCount(1,
            "the catalogue offers one starting sheet and not two");
    }

    /// <summary>
    /// The site is never left without a starting sheet, so the removal of the
    /// default one touches nothing.
    /// </summary>
    [Fact]
    public async Task RefuseToHideTheDefaultTemplate()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(dbContext);
        var author = await SeedUser(dbContext);
        var created = await repository.Create(Creation(UniqueTitle()), author);
        await repository.MakeDefault(created.Id);

        (await repository.Delete(created.Id)).Should().Be(
            AttributeSchemaTemplateRemoval.RefusedAsDefault);

        var still = await repository.GetTemplate(created.Id);
        still.Should().NotBeNull("the refused removal wrote nothing");
        still!.IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// A hidden template cannot become the default one, whichever order the two
    /// writes arrive in: the assignment requires a live row.
    /// </summary>
    /// <remarks>
    /// Another template holds the flag while the refusal happens, because the
    /// assignment takes the flag off its holder before it learns the target is
    /// hidden. With nothing holding it there is nothing for the rollback to put
    /// back, and the assertion passes on a write that kept the half it managed.
    /// </remarks>
    [Fact]
    public async Task RefuseTheDefaultFlagToAHiddenTemplate()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(dbContext);
        var author = await SeedUser(dbContext);
        var standing = await repository.Create(Creation(UniqueTitle()), author);
        var hidden = await repository.Create(Creation(UniqueTitle()), author);
        await repository.MakeDefault(standing.Id);
        await repository.Delete(hidden.Id);

        (await repository.MakeDefault(hidden.Id)).Should().BeFalse();

        (await LiveDefaults(dbContext)).Should().ContainSingle()
            .Which.Should().Be(standing.Id,
                "the refused assignment put back the flag it had already taken off");
    }

    /// <summary>
    /// The database itself refuses a second live default, which is what makes
    /// "at most one" a rule rather than a habit of the write path.
    /// </summary>
    [Fact]
    public async Task RefuseASecondLiveDefaultRow()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(dbContext);
        var author = await SeedUser(dbContext);
        var first = await repository.Create(Creation(UniqueTitle()), author);
        var second = await repository.Create(Creation(UniqueTitle()), author);

        await repository.MakeDefault(first.Id);

        var byHand = async () => await dbContext.AttributeSchemaTemplates
            .Where(t => t.TemplateId == second.Id)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsDefault, true));

        await byHand.Should().ThrowAsync<PostgresException>(
            "the partial unique index is what keeps a second starting sheet out");
    }

    /// <summary>
    /// Two assignments running at once leave one default behind, and it is one
    /// of the two that asked for it - the loser is refused by the index rather
    /// than left overwriting the winner.
    /// </summary>
    [Fact]
    public async Task LeaveOneDefaultWhenTwoAssignmentsRaceEachOther()
    {
        using var setup = DatabaseFixture.Factory.Services.CreateScope();
        var setupRepository = setup.ServiceProvider
            .GetRequiredService<IAttributeSchemaTemplateRepository>();
        var setupContext = setup.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(setupContext);
        var author = await SeedUser(setupContext);
        var standing = await setupRepository.Create(Creation(UniqueTitle()), author);
        var first = await setupRepository.Create(Creation(UniqueTitle()), author);
        var second = await setupRepository.Create(Creation(UniqueTitle()), author);
        await setupRepository.MakeDefault(standing.Id);

        using var firstScope = DatabaseFixture.Factory.Services.CreateScope();
        using var secondScope = DatabaseFixture.Factory.Services.CreateScope();

        var refusals = await Task.WhenAll(
            Assign(firstScope, first.Id),
            Assign(secondScope, second.Id));

        refusals.Where(refusal => refusal != null).Should().AllBeOfType<DuplicateEntityException>(
            "an assignment that loses the race is refused by the partial unique index");

        var defaults = await LiveDefaults(setupContext);
        defaults.Should().HaveCount(1,
            "however the two land, the catalogue keeps exactly one starting sheet");
        new[] { first.Id, second.Id }.Should().Contain(defaults.Single(),
            "one of the two asked for the flag and got it, so the template that held " +
            "it before is not the one still holding it");
    }

    private static async Task<Exception?> Assign(IServiceScope scope, Guid templateId)
    {
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        try
        {
            await repository.MakeDefault(templateId);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// The assignment that arrives second is refused by the partial unique
    /// index, and that refusal reaches the service as a duplicate rather than
    /// as a bare database error.
    /// </summary>
    /// <remarks>
    /// The loser is held still instead of being raced: the winning claim is made
    /// by hand in a transaction that stays open, the assignment is let into the
    /// database and parked on it, and only then is the claim committed. A race
    /// shows this on the runs where it happens to interleave and nothing at all
    /// on the rest.
    ///
    /// The claim runs on a connection of its own rather than through a context,
    /// because the host configures a retrying execution strategy and that
    /// strategy refuses a transaction opened outside it.
    /// </remarks>
    [Fact]
    public async Task RefuseAnAssignmentTheFlagWasClaimedAheadOf()
    {
        using var setup = DatabaseFixture.Factory.Services.CreateScope();
        var setupRepository = setup.ServiceProvider
            .GetRequiredService<IAttributeSchemaTemplateRepository>();
        var setupContext = setup.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(setupContext);
        var author = await SeedUser(setupContext);
        var standing = await setupRepository.Create(Creation(UniqueTitle()), author);
        var winner = await setupRepository.Create(Creation(UniqueTitle()), author);
        var loser = await setupRepository.Create(Creation(UniqueTitle()), author);
        await setupRepository.MakeDefault(standing.Id);

        await using var claimConnection = new NpgsqlConnection(DatabaseFixture.ConnectionString);
        await claimConnection.OpenAsync();
        await using var claim = await claimConnection.BeginTransactionAsync();
        await ClaimTheDefaultFlag(claimConnection, claim, winner.Id);

        using var loserScope = DatabaseFixture.Factory.Services.CreateScope();
        var loserContext = loserScope.ServiceProvider.GetRequiredService<DmDbContext>();
        await loserContext.Database.OpenConnectionAsync();
        var loserBackend = ((NpgsqlConnection)loserContext.Database.GetDbConnection()).ProcessID;

        var assignment = Task.Run(() => Assign(loserScope, loser.Id));
        await WaitUntilTheAssignmentWaits(DatabaseFixture.ConnectionString, loserBackend);
        await claim.CommitAsync();

        (await assignment).Should().BeOfType<DuplicateEntityException>(
            "the flag was gone by the time the assignment reached for it, and the " +
            "index is what said so");

        (await LiveDefaults(setupContext)).Should().ContainSingle()
            .Which.Should().Be(winner.Id, "the refused assignment wrote nothing");
    }

    /// <summary>
    /// The pair of writes an assignment is made of, run by hand so that the pair
    /// can be left hanging between the first and the commit.
    /// </summary>
    private static async Task ClaimTheDefaultFlag(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid templateId)
    {
        await Write(connection, transaction,
            """
            update "AttributeSchemaTemplates" set "IsDefault" = false
            where "IsDefault" and not "IsRemoved" and "TemplateId" <> @templateId
            """, templateId);
        await Write(connection, transaction,
            """
            update "AttributeSchemaTemplates" set "IsDefault" = true
            where not "IsRemoved" and "TemplateId" = @templateId
            """, templateId);
    }

    private static async Task Write(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid templateId)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("templateId", templateId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Waits for the assignment to reach the database and stop there. The claim
    /// it collides with is not committed yet, so PostgreSQL parks the assignment
    /// rather than refusing it; committing before it is parked would let it take
    /// the flag off the holder and keep it, and the test would prove nothing.
    /// </summary>
    private static async Task WaitUntilTheAssignmentWaits(string connectionString, int backendPid)
    {
        await using var watch = new NpgsqlConnection(connectionString);
        await watch.OpenAsync();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                "select cardinality(pg_blocking_pids(@backendPid))", watch);
            command.Parameters.AddWithValue("backendPid", backendPid);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        throw new InvalidOperationException(
            "The assignment never waited on the claim it has to lose to");
    }

    /// <summary>
    /// The catalogue leads with the template the creation form starts from,
    /// and orders the rest by name.
    /// </summary>
    [Fact]
    public async Task LeadTheCatalogueWithTheDefaultTemplate()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await ClearTheDefaultFlag(dbContext);
        var author = await SeedUser(dbContext);
        var last = await repository.Create(Creation("Яшмовый шаблон"), author);
        await repository.MakeDefault(last.Id);
        await repository.Create(Creation("Алмазный шаблон"), author);

        var catalogue = (await repository.GetTemplates()).ToList();

        catalogue.First().Id.Should().Be(last.Id,
            "the default template is offered first however its name sorts");
        catalogue.Skip(1).Select(t => t.Title)
            .Should().BeInAscendingOrder(StringComparer.Ordinal.Compare,
                "the rest of the catalogue is ordered by name");
        catalogue.Skip(1).Should().AllSatisfy(t => t.IsDefault.Should().BeFalse());
    }

    /// <summary>
    /// The catalogue carries the documents, so the creation form opens on one
    /// request.
    /// </summary>
    [Fact]
    public async Task CarryTheDocumentsInTheCatalogue()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttributeSchemaTemplateRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var author = await SeedUser(dbContext);
        var created = await repository.Create(Creation(UniqueTitle()), author);

        var listed = (await repository.GetTemplates()).Single(t => t.Id == created.Id);

        listed.Specifications.Should().ContainSingle()
            .Which.Title.Should().Be("Сила");
    }

    private static async Task<List<Guid>> LiveDefaults(DmDbContext dbContext) =>
        await dbContext.AttributeSchemaTemplates
            .AsNoTracking()
            .Where(t => t.IsDefault)
            .Select(t => t.TemplateId)
            .ToListAsync();

    private static async Task<Guid> SeedUser(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"m{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }
}
