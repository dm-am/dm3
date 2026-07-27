using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Shared schema-level validation rules for attribute specifications.
/// Reused by both create and update schema validators so every persistence
/// path enforces the same invariants.
/// </summary>
internal static class AttributeSpecificationRules
{
    /// <summary>
    /// Collect all rule violations for a set of specifications.
    /// </summary>
    /// <param name="specifications">Specifications being persisted (may be null on update)</param>
    /// <returns>Human-readable error messages, empty when valid</returns>
    public static IEnumerable<string> Collect(IEnumerable<IAttributeSpecificationInput>? specifications)
    {
        if (specifications == null)
        {
            yield break;
        }

        var specs = specifications.ToList();

        // At most one descriptor across the whole schema
        if (specs.Count(s => s.IsDescriptor) > 1)
        {
            yield return "Only one attribute may be marked as descriptor";
        }

        foreach (var spec in specs)
        {
            var title = string.IsNullOrWhiteSpace(spec.Title) ? "(untitled)" : spec.Title.Trim();

            // Non-empty title per spec
            if (string.IsNullOrWhiteSpace(spec.Title))
            {
                yield return "Attribute title must not be empty";
            }

            // BbCode specs may not be descriptor
            if (spec.Type == AttributeSpecificationType.BbCode && spec.IsDescriptor)
            {
                yield return $"BBCode attribute '{title}' cannot be a descriptor";
            }

            if (!IsListType(spec.Type))
            {
                continue;
            }

            var values = spec.Values?.ToList() ?? [];

            // List types require at least one value
            if (values.Count == 0)
            {
                yield return $"List attribute '{title}' requires at least one value";
                continue;
            }

            // List option values must be unique per spec (read-time text matching relies on it)
            var distinctCount = values.Select(v => v.Value?.Trim()).Distinct().Count();
            if (distinctCount != values.Count)
            {
                yield return $"List attribute '{title}' must not contain duplicate values";
            }

            // Text-number list requires both value and modifier for each option
            if (spec.Type == AttributeSpecificationType.TextNumberList &&
                values.Any(v => string.IsNullOrWhiteSpace(v.Value) || v.Modifier == null))
            {
                yield return $"Text-number list '{title}' requires both a value and a modifier for each option";
            }
        }
    }

    private static bool IsListType(AttributeSpecificationType type) =>
        type is AttributeSpecificationType.TextList
            or AttributeSpecificationType.NumberList
            or AttributeSpecificationType.TextNumberList;
}
