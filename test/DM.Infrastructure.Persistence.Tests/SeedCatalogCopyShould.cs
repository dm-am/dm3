using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Infrastructure.Persistence.Entities.Community;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// A seeded catalog description says what the badge is given for and addresses
/// nobody.
/// </summary>
/// <remarks>
/// Awards and achievement categories are drawn on every profile, so the text
/// under a badge is read by everyone who opens the page and not only by the
/// person holding it. Six category descriptions used to speak to that person
/// directly ("твоих игровых постов", "ты покинул"), and four of those also fixed
/// the reader as male, on a site that keeps gender as a profile field.
///
/// The rows are read out of the design-time model rather than restated here, so
/// the rule follows the text instead of a copy of it. The three migration
/// artefacts carry the same rows in generated form and are edited together with
/// the model.
///
/// Titles are out of scope on purpose: they are jokes, and a joke is not held to
/// the register of a description.
/// </remarks>
public class SeedCatalogCopyShould
{
    /// <summary>
    /// Second person, form by form. A list and not a stem: "тво" also opens
    /// "творчество", and the oblique cases share no prefix with the nominative.
    /// The plural is here as well, because the rule is not "no ты" but "no reader
    /// on the other end of the phrase".
    /// </summary>
    private static readonly HashSet<string> AddressPronouns = new(StringComparer.Ordinal)
    {
        "ты", "тебя", "тебе", "тобой", "тобою",
        "твой", "твоего", "твоему", "твоим", "твоем",
        "твоя", "твоей", "твою", "твоею",
        "твое", "твои", "твоих", "твоими",
        "вы", "вас", "вам", "вами",
        "ваш", "вашего", "вашему", "вашим", "вашем",
        "ваша", "вашей", "вашу", "ваше",
        "ваши", "ваших", "вашими",
    };

    /// <summary>
    /// A non-past verb in the second person singular ends in -shj, with or
    /// without the reflexive particle: "считаешь", "получишь", "считаешься". An
    /// ending rather than a list of verbs, so the nouns shaped the same way are
    /// named below instead of being guessed at.
    /// </summary>
    private static readonly string[] AddressVerbEndings = ["ешь", "ишь", "ешься", "ишься"];

    /// <summary>Nouns that end like such a verb and address no one.</summary>
    private static readonly HashSet<string> NounsShapedLikeAVerb = new(StringComparer.Ordinal)
    {
        "брешь", "плешь", "тишь",
    };

    /// <summary>
    /// Characters CODE_STYLE keeps out of the text a person reads, seeded data
    /// included, each with the reason it stays out.
    /// </summary>
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

    /// <summary>A word: a run of letters, folded to lower case before lookup.</summary>
    private static readonly Regex Word = new(@"\p{L}+", RegexOptions.Compiled);

    [Fact]
    public void AddressNobody()
    {
        var descriptions = SeededDescriptions();
        descriptions.Should().NotBeEmpty("a rule that matches nothing passes");

        var addressed = descriptions
            .Select(row => new
            {
                row.Badge,
                row.Text,
                Forms = Words(row.Text).Where(IsAddress).Distinct(StringComparer.Ordinal).ToArray(),
            })
            .Where(row => row.Forms.Length > 0)
            .Select(row => $"{row.Badge}: {string.Join(", ", row.Forms)} in \"{row.Text}\"")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

        addressed.Should().BeEmpty(
            "the description is read by everyone who opens the profile, not only by the holder of the badge");
    }

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

    private static bool IsAddress(string word) =>
        AddressPronouns.Contains(word)
        || (!NounsShapedLikeAVerb.Contains(word)
            && AddressVerbEndings.Any(ending =>
                word.Length > ending.Length && word.EndsWith(ending, StringComparison.Ordinal)));

    private static IEnumerable<string> Words(string text) =>
        Word.Matches(text).Select(match => match.Value.ToLowerInvariant());

    private static IReadOnlyCollection<(string Badge, string Text)> SeededDescriptions()
    {
        // Model metadata only: the connection is never opened.
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql("Host=localhost;Database=seed-copy-probe;Username=probe;Password=probe")
            .Options;
        using var context = new DmDbContext(options);

        // HasData lives in the design-time model; the runtime one drops it.
        var model = context.GetService<IDesignTimeModel>().Model;

        return DescriptionsOf<AwardType>(
                model, nameof(AwardType.Code), nameof(AwardType.Description))
            .Concat(DescriptionsOf<AchievementCategory>(
                model, nameof(AchievementCategory.Code), nameof(AchievementCategory.Description)))
            .ToList();
    }

    private static IEnumerable<(string Badge, string Text)> DescriptionsOf<TEntity>(
        IModel model, string codeProperty, string descriptionProperty) =>
        model.FindEntityType(typeof(TEntity))!
            .GetSeedData()
            .Select(row => (
                Badge: $"{typeof(TEntity).Name} {row[codeProperty]}",
                Text: (string)row[descriptionProperty]!));
}
