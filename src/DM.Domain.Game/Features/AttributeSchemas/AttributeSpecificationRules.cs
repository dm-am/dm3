using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Shared schema-level validation rules for attribute specifications.
/// Reused by both create and update schema validators so every persistence
/// path enforces the same invariants.
/// </summary>
internal static class AttributeSpecificationRules
{
    /// <summary>
    /// The per-specification rules, whichever DTO carries the specification.
    /// </summary>
    /// <remarks>
    /// Written once and applied by both schema validators: creation and editing
    /// have to accept the same specification, and the copy that used to sit in
    /// each of them was the same three rules word for word.
    /// </remarks>
    public static void Apply<TSpecification>(InlineValidator<TSpecification> specification)
        where TSpecification : IAttributeSpecificationInput
    {
        specification.RuleFor(s => s.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(AttributeSchemaFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        specification.RuleFor(s => s.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        specification.RuleFor(s => s.Order)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// Report every schema-level violation of <see cref="Collect" /> to the
    /// validation context of whichever DTO is being checked.
    /// </summary>
    public static void AddFailures<TSchema>(
        IEnumerable<IAttributeSpecificationInput>? specifications,
        ValidationContext<TSchema> context)
    {
        foreach (var error in Collect(specifications))
        {
            context.AddFailure(error);
        }
    }

    /// <summary>
    /// Report every repeated identifier of <see cref="CollectRepeatedIdentifiers" />
    /// to the validation context of whichever DTO is being checked.
    /// </summary>
    public static void AddRepeatedIdentifierFailures<TSchema>(
        IEnumerable<Guid?>? identifiers,
        ValidationContext<TSchema> context)
    {
        foreach (var error in CollectRepeatedIdentifiers(identifiers))
        {
            context.AddFailure(error);
        }
    }

    /// <summary>
    /// Identifiers submitted twice in one set.
    /// </summary>
    /// <remarks>
    /// Identifiers inside a schema are unique, and a replacement keeps the ones
    /// the stored set already holds, so a set naming the same one twice asks
    /// for two specifications that are the same specification, and the values of
    /// the characters written against it would answer to both. Refused here,
    /// before anything is written, rather than settled by whichever of the two
    /// the document happens to keep.
    ///
    /// An absent or empty identifier is not a repeat: those are the spelling of
    /// "new specification", and the server hands each of them one of its own.
    /// </remarks>
    public static IEnumerable<string> CollectRepeatedIdentifiers(IEnumerable<Guid?>? identifiers)
    {
        if (identifiers == null)
        {
            yield break;
        }

        var repeated = identifiers
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .GroupBy(id => id)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .ToArray();

        if (repeated.Length > 0)
        {
            yield return AttributeValidationError.ManyDuplicated(repeated);
        }
    }

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
            yield return "Описателем можно отметить только один атрибут";
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
                yield return $"Атрибут '{title}' с разметкой не может быть описателем";
            }

            if (!IsListType(spec.Type))
            {
                continue;
            }

            var values = spec.Values?.ToList() ?? [];

            // List types require at least one value
            if (values.Count == 0)
            {
                yield return $"У списка '{title}' должно быть хотя бы одно значение";
                continue;
            }

            // List option values must be unique per spec (read-time text matching relies on it)
            var distinctCount = values.Select(v => v.Value?.Trim()).Distinct().Count();
            if (distinctCount != values.Count)
            {
                yield return $"В списке '{title}' значения не должны повторяться";
            }

            // Text-number list requires both value and modifier for each option
            if (spec.Type == AttributeSpecificationType.TextNumberList &&
                values.Any(v => string.IsNullOrWhiteSpace(v.Value) || v.Modifier == null))
            {
                yield return $"В списке '{title}' у каждого варианта нужны и значение, и модификатор";
            }
        }
    }

    private static bool IsListType(AttributeSpecificationType type) =>
        type is AttributeSpecificationType.TextList
            or AttributeSpecificationType.NumberList
            or AttributeSpecificationType.TextNumberList;
}
