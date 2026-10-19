using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// How many tags of one group a game may carry.
/// </summary>
/// <remarks>
/// A column of the tag group, not a constant: moderation edits the catalogue without a release.
/// Both write paths reach it through <see cref="IGameTagResolver.ResolveTagIds"/>.
/// </remarks>
internal static class TagGroupLimit
{
    private const string TagsField = "tags";

    /// <remarks>
    /// Every group over its number is named, so the master fixes the form once.
    /// </remarks>
    /// <exception cref="HttpBadRequestException">A group is over its number</exception>
    public static void ThrowIfExceeded(IReadOnlyCollection<GameTag> selectedTags)
    {
        var exceeded = selectedTags
            .Where(t => t.GroupMaxTagsPerGame.HasValue)
            .GroupBy(t => t.GroupTitle, StringComparer.Ordinal)
            .Where(g => g.Count() > g.First().GroupMaxTagsPerGame!.Value)
            // In the order the form draws the groups.
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

    private static string Refusal(string groupTitle, int limit) => limit == 1
        ? $"В группе \"{groupTitle}\" можно выбрать только один тег"
        : $"В группе \"{groupTitle}\" можно выбрать не больше {limit} {TagsWord(limit)}";

    /// <summary>
    /// The genitive after a number: "21 тега", "22 тегов", "11 тегов".
    /// </summary>
    private static string TagsWord(int count) =>
        count % 10 == 1 && count % 100 != 11 ? "тега" : "тегов";
}
