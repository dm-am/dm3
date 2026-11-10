using System;
using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Testing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

// The text under a badge is read by everyone who opens the profile.
public class SeedCatalogCopyShould
{
    private static readonly IReadOnlyDictionary<char, string> ForbiddenCharacters =
        new Dictionary<char, string>
        {
            ['\u0451'] = "the letter with two dots: the site spells it as a plain е",
            ['Ё'] = "the same letter capitalised",
            ['—'] = "em dash reads as machine text: a comma, a colon or brackets carry it",
            ['–'] = "en dash, for the same reason",
            ['…'] = "the ellipsis is three dots, U+2026 belongs to truncation markers",
            ['«'] = "typographic quotes are reserved for the motto",
            ['»'] = "typographic quotes are reserved for the motto",
            ['“'] = "typographic quotes are reserved for the motto",
            ['”'] = "typographic quotes are reserved for the motto",
            ['„'] = "typographic quotes are reserved for the motto",
            ['‘'] = "typographic quotes are reserved for the motto",
            ['’'] = "typographic quotes are reserved for the motto",
            ['·'] = "the middle dot is not a separator in interface copy",
            [';'] = "a semicolon in interface copy is either two phrases or a comma",
        };

    [Fact]
    public void SpellTextWithTheCharactersTheSiteAllows()
    {
        var descriptions = SeededDescriptions();
        descriptions.Should().NotBeEmpty("a rule that matches nothing passes");

        var violations = descriptions
            .SelectMany(row => row.Text
                .Where(ForbiddenCharacters.ContainsKey)
                .Distinct()
                .Select(character => $"{row.Badge}: U+{(int)character:X4}, {ForbiddenCharacters[character]}"))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty("the rules for Russian text hold for seeded data as well");
    }

    [Fact]
    public void StayOnePhrase()
    {
        var descriptions = SeededDescriptions();
        descriptions.Should().NotBeEmpty("a rule that matches nothing passes");

        var dotted = descriptions
            .Where(row => row.Text.Contains('.'))
            .Select(row => $"{row.Badge}: {row.Text}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

        dotted.Should().BeEmpty(
            "the rest of the seed (boards, tag groups, tags) carries one phrase per description and no closing dot");
    }

    private static IReadOnlyCollection<(string Badge, string Text)> SeededDescriptions() =>
        DescriptionsOf<AwardType>(nameof(AwardType.Code), nameof(AwardType.Description))
            .Concat(DescriptionsOf<AchievementCategory>(
                nameof(AchievementCategory.Code), nameof(AchievementCategory.Description)))
            .ToList();

    private static IEnumerable<(string Badge, string Text)> DescriptionsOf<TEntity>(
        string codeProperty, string descriptionProperty) =>
        ProbeContext.SeedRows<TEntity>()
            .Select(row => (
                Badge: $"{typeof(TEntity).Name} {row[codeProperty]}",
                Text: (string)row[descriptionProperty]!));
}
