using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// How many tags of one group a game may carry.
/// </summary>
/// <remarks>
/// The number is a column of the tag group and not a constant here: moderation
/// edits the catalogue, and raising "Жанр" from three to four must not need a
/// release. A group with no number set limits nothing.
///
/// One implementation for both write paths on purpose. Creation and the settings
/// page reach it through <see cref="IGameCreationDataResolver.ResolveTagIds"/>,
/// which is the single place a submitted set of tags is read, so the two cannot
/// drift into two rules.
/// </remarks>
internal static class TagGroupLimit
{
    /// <summary>
    /// The request field the refusal belongs to, spelled as the API spells it.
    /// </summary>
    private const string TagsField = "tags";

    /// <summary>
    /// Refuse a set that takes more tags out of a group than the group allows.
    /// </summary>
    /// <remarks>
    /// Every group over its number is named, not just the first: the master sees
    /// one refusal and fixes the form once.
    /// </remarks>
    /// <param name="selectedTags">The catalogue entries the submitted set resolved to</param>
    /// <exception cref="HttpBadRequestException">A group is over its number</exception>
    public static void ThrowIfExceeded(IReadOnlyCollection<GameTag> selectedTags)
    {
        var exceeded = selectedTags
            .Where(t => t.GroupMaxTagsPerGame.HasValue)
            .GroupBy(t => t.GroupTitle, StringComparer.Ordinal)
            .Where(g => g.Count() > g.First().GroupMaxTagsPerGame!.Value)
            // The catalogue's own order, so two groups over their number are
            // named in the order the form draws them.
            .OrderBy(g => g.First().GroupSortOrder)
            .Select(g => Refusal(g.Key, g.First().GroupMaxTagsPerGame!.Value))
            .ToList();

        if (exceeded.Count == 0)
        {
            return;
        }

        var refusal = string.Join(". ", exceeded);

        throw new HttpBadRequestException(new Dictionary<string, string>
        {
            [TagsField] = refusal
        });
    }

    /// <summary>
    /// The refusal for one group, naming the group and its number.
    /// </summary>
    private static string Refusal(string groupTitle, int limit) => limit == 1
        ? $"В группе \"{groupTitle}\" можно выбрать только один тег"
        : $"В группе \"{groupTitle}\" можно выбрать не больше {limit} {TagsWord(limit)}";

    /// <summary>
    /// The form of "тег" a number takes in the genitive: "21 тега" against
    /// "22 тегов" and "11 тегов".
    /// </summary>
    private static string TagsWord(int count) =>
        count % 10 == 1 && count % 100 != 11 ? "тега" : "тегов";
}
