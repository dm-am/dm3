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
/// Indexes EF cannot express are asserted by the application at startup rather than written
/// into the migration by hand, which is what keeps the migration safe to regenerate. Two of
/// them stop accounts differing only by letter case and turn a login lookup into an index
/// scan; one stops a second name change request racing past the check that reads before it
/// writes.
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
    /// Whatever the declaration says, uniqueness is what every index in this set exists for.
    /// </summary>
    /// <remarks>
    /// The check above compares two strings and would go on passing if both of them were
    /// changed together into an index that constrains nothing. This is the part that cannot
    /// be satisfied by agreement.
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
    /// Two accounts differing only by letter case are two accounts nobody can tell apart,
    /// and no later repair can decide which of them owns the address. The lookup compares
    /// lower(column) to lower(value), so an index over the raw column serves neither that
    /// predicate nor the invariant.
    /// </remarks>
    [Theory]
    [InlineData("IX_Users_Email_Lower")]
    [InlineData("IX_Users_Username_Lower")]
    public void FoldCaseWhereTwoSpellingsAreOneAccount(string indexName)
    {
        var declared = ExpressionIndexInitializer.Declared.Single(index => index.Name == indexName);

        declared.Definition.Should().Contain("lower");
    }

    /// <summary>
    /// The name change index constrains requests awaiting a moderator and nothing else.
    /// </summary>
    /// <remarks>
    /// Without the predicate the account could file one request in its lifetime: every
    /// finished request would keep the slot. And the predicate cannot be widened to include
    /// approvals, because an approval stops being in flight when its link runs out rather
    /// than when its status changes, and no index predicate can be written against the
    /// clock - that half is the service's to judge.
    /// </remarks>
    [Fact]
    public void ConstrainOnlyTheNameChangeRequestsThatAwaitAModerator()
    {
        var declared = ExpressionIndexInitializer.Declared
            .Single(index => index.Name == "IX_UsernameChangeRequests_UserId_Pending");

        declared.Definition.Should().Contain("""WHERE ("Status" = 0)""");
    }
}
