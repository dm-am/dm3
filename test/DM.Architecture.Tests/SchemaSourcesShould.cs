using System.IO;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The model the schema rules read is the model and nothing beside it.
/// </summary>
/// <remarks>
/// Two rules let the files of the model spell what every other file is refused
/// (the newbie threshold and the ticket conversation), so the predicate that
/// picks them is an exemption, and an exemption that widens is a rule that
/// quietly stops applying. It has to follow the model when the context is cut
/// into a file per module, and stop there.
/// </remarks>
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
