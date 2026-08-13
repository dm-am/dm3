using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.RelationalStorage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The unique indexes over lower(Email) and lower(Username) are what make a login lookup an
/// index scan instead of a sequential one, and what stops two accounts differing only by
/// letter case. EF cannot express an expression index, so they are asserted by the
/// application at startup rather than written into the migration by hand — which is what
/// keeps the migration safe to regenerate.
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
    /// Compared against the whole definition, and against the same text the startup hook
    /// compares against, because both halves of that need a server to be true. Postgres
    /// matches CREATE INDEX IF NOT EXISTS on the name alone: an index of this name that is
    /// not unique, or not over lower(), or over another column entirely is reported as
    /// already there and the statement succeeds. So the hook reads the definition back — and
    /// the text it expects is a literal that no compiler checks, normalised by the server in
    /// ways nobody writes from memory (the schema, the access method, the cast of the
    /// argument). Wrong, it makes the hook report a healthy database as broken on every
    /// start, forever, and this is what says so.
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
    /// Whatever the declaration says, these two properties are the point of it.
    /// </summary>
    /// <remarks>
    /// The check above compares two strings and would go on passing if both of them were
    /// changed together into something that indexes the raw column. This is the part that
    /// cannot be satisfied by agreement: unique, and over the lowered value.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Declared))]
    public void CarryTheInvariantItExistsFor(string indexName)
    {
        var declared = ExpressionIndexInitializer.Declared.Single(index => index.Name == indexName);

        declared.Definition.Should().Contain("UNIQUE",
            "two accounts differing only by letter case are two accounts nobody can tell " +
            "apart, and no later repair can decide which of them owns the address");
        declared.Definition.Should().Contain("lower",
            "the lookup compares lower(column) to lower(value), and an index over the raw " +
            "column serves neither that predicate nor the invariant");
    }
}
