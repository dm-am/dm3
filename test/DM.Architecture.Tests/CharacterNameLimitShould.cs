using System.Globalization;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

// Read out of the sources: the migration and snapshots build the database, the client cannot be
// asked.
public class CharacterNameLimitShould
{
    private const string CharacterEntity =
        "modelBuilder.Entity(\"DM.Infrastructure.Persistence.Entities.Game.Characters.Character\", b =>";

    private static readonly Regex ClientLimit =
        new(@"CHARACTER_NAME_MAX_LENGTH\s*=\s*(\d+)", RegexOptions.Compiled);

    [Fact]
    public void BeSpelledOnceForBothValidators()
    {
        foreach (var validator in new[] { "CreateCharacterValidator.cs", "UpdateCharacterValidator.cs" })
        {
            var source = RepositoryFiles.Read("src", "DM.Domain.Game", "Features", "Characters", validator);

            source.Should().Contain("MaximumLength(CharacterPolicy.NameMaxLength)",
                $"{validator} answers the save, and the number it answers with belongs to the policy");
            Regex.IsMatch(source, @"MaximumLength\(\d").Should().BeFalse(
                $"a length spelled out in {validator} is a second decision, and it drifts from the " +
                "column and from the form the first time the product one moves");
        }
    }

    [Fact]
    public void BeTheWidthOfTheStoredColumn()
    {
        var limit = CharacterPolicy.NameMaxLength;

        var migration = SchemaSources.Migration;
        SourceText.Between(migration, "name: \"Characters\",", "constraints: table =>").Should().Contain(
            $"Name = table.Column<string>(type: \"character varying({limit})\", maxLength: {limit}",
            "the column is the third place the limit lives, and the only one the other two cannot talk out of it");

        foreach (var (snapshot, text) in SchemaSources.Snapshots)
        {
            var block = SourceText.Between(
                text,
                CharacterEntity, "b.ToTable(\"Characters\");");

            block.Should().Contain($".HasMaxLength({limit})",
                $"{snapshot} describes the same column, and a snapshot disagreeing with the migration " +
                "is a schema nobody can rebuild");
            block.Should().Contain($".HasColumnType(\"character varying({limit})\")",
                $"{snapshot} describes the same column");
        }
    }

    [Fact]
    public void BeTheNumberTheFormStopsTypingAt()
    {
        var declared = ClientLimit.Match(RepositoryFiles.Read(
            "src", "DM.Web.Client", "src", "shared", "lib", "constants", "game.ts"));

        declared.Success.Should().BeTrue(
            "the client keeps its own copy of the limit and has to declare it as a number");
        int.Parse(declared.Groups[1].Value, CultureInfo.InvariantCulture).Should().Be(
            CharacterPolicy.NameMaxLength,
            "a form that accepts more than the validator does turns a finished name into a refusal");
    }
}
