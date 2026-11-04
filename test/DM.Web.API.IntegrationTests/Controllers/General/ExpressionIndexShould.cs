using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.RelationalStorage;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Indexes EF cannot express are asserted by the application at startup, so the migration stays safe to
/// regenerate.
/// </summary>
public class ExpressionIndexShould : IntegrationTestBase
{
    public ExpressionIndexShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The index set the application declares, walked rather than repeated.</summary>
    public static TheoryData<string> Declared()
    {
        var names = new TheoryData<string>();
        foreach (var index in ExpressionIndexInitializer.Declared)
        {
            names.Add(index.Name);
        }

        return names;
    }

    /// <summary>
    /// The index exists, and it is the index the application thinks it is.
    /// </summary>
    /// <remarks>
    /// Postgres matches CREATE INDEX IF NOT EXISTS on the name alone, so the hook reads the definition back, as a
    /// literal the server normalises; compared here against the same text.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Declared))]
    public async Task MatchTheDefinitionTheApplicationAsserts(string indexName)
    {
        var declared = ExpressionIndexInitializer.Declared.Single(index => index.Name == indexName);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var definitions = await dbContext.Database
            .SqlQuery<string>($"""SELECT indexdef FROM pg_indexes WHERE indexname = {indexName}""")
            .ToListAsync();

        definitions.Should().ContainSingle(
            "the startup initializer asserts this index and re-asserts it on every start");
        definitions.Single().Should().Be(declared.Definition,
            "the hook compares the definition it reads back against this exact text, so a " +
            "difference here is either an index nobody would want or a hook that reports a " +
            "healthy database as broken on every start");
    }

    /// <summary>
    /// Whatever the declaration says, uniqueness is what every index in this set exists for.
    /// </summary>
    /// <remarks>
    /// The check above passes when the statement and the definition change together, so the
    /// invariants below are the part that agreement cannot satisfy.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Declared))]
    public void CarryTheInvariantItExistsFor(string indexName)
    {
        var declared = ExpressionIndexInitializer.Declared.Single(index => index.Name == indexName);

        declared.Definition.Should().Contain("UNIQUE",
            "every index declared here exists to refuse a duplicate, and one that only " +
            "speeds a lookup up would let the duplicate through while looking the same");
    }

    /// <summary>
    /// The case-folding pair indexes the lowered value, not the raw column.
    /// </summary>
    /// <remarks>
    /// The lookup compares lower(column) to lower(value), and an index over the raw column
    /// serves neither that predicate nor the rule that two spellings are one account.
    /// </remarks>
    [Theory]
    [InlineData("IX_Users_Email_Lower")]
    [InlineData("IX_Users_Username_Lower")]
    public void FoldCaseWhereTwoSpellingsAreOneAccount(string indexName)
    {
        var declared = ExpressionIndexInitializer.Declared.Single(index => index.Name == indexName);

        declared.Definition.Should().Contain("lower",
            "two accounts differing only by letter case are two accounts nobody can tell apart");
    }
}
