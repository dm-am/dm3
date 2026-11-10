using System.IO;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

// The predicate is an exemption, and one that widens quietly stops a rule applying.
public class SchemaSourcesShould
{
    private const string Entry = "protected override void OnModelCreating(ModelBuilder modelBuilder) { }";

    private const string TakesABuilder = "internal static void Configure(ModelBuilder modelBuilder, bool isPostgres) { }";

    [Theory]
    [InlineData("DmDbContext.cs", Entry, true)]
    [InlineData("ModelConfiguration/AccountModel.cs", TakesABuilder, true)]
    [InlineData("Repositories/Moderation/TicketRepository.cs", TakesABuilder, false)]
    [InlineData("ProbeModelSplit.cs", TakesABuilder, false)]
    [InlineData("InitialData/TagCatalog.cs", "internal static class TagCatalog { }", false)]
    [InlineData("InitialData/DefaultAttributeSchema.cs", "internal static class DefaultAttributeSchema { }", false)]
    public void TakeTheContextAndTheModuleFilesAndNothingElse(string relativePath, string code, bool isModel) =>
        SchemaSources.IsModelFile(relativePath.Replace('/', Path.DirectorySeparatorChar), code).Should().Be(isModel);
}
