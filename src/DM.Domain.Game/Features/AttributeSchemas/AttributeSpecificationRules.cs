using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// Schema rules shared by every validator that saves specifications, so all paths agree.
/// </summary>
internal static class AttributeSpecificationRules
{
    /// <summary>
    /// The per-specification rules, whichever DTO carries the specification.
    /// </summary>
    public static void Apply<TSpecification>(InlineValidator<TSpecification> specification)
        where TSpecification : CreateAttributeSpecification
    {
        specification.RuleFor(s => s.Title)
            .NotEmpty().WithMessage("Введите название атрибута")
            .MaximumLength(AttributeSchemaFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        specification.RuleFor(s => s.Type)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        specification.RuleFor(s => s.Order)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);

        // Absent means "no limit"; a limit below one admits no value at all.
        specification.RuleFor(s => s.MaxLength)
            .GreaterThan(0).WithMessage(ValidationError.MustBePositive)
            .When(s => s.MaxLength.HasValue);
    }

    /// <summary>
    /// The per-specification rules and the schema-level ones, for whichever DTO carries the set.
    /// </summary>
    public static void RuleForSpecifications<TSchema, TSpecification>(
        this AbstractValidator<TSchema> validator,
        Expression<Func<TSchema, IEnumerable<TSpecification>>> specifications)
        where TSpecification : CreateAttributeSpecification
    {
        validator.RuleForEach(specifications).ChildRules(Apply);
        validator.RuleFor(specifications).Custom(AddFailures);
    }

    /// <summary>
    /// Report every schema-level violation of <see cref="Collect" /> to the
    /// validation context of whichever DTO is being checked.
    /// </summary>
    private static void AddFailures<TSchema>(
        IEnumerable<CreateAttributeSpecification>? specifications,
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
    /// Two specifications under one identifier would both own the values written against it.
    /// An absent or empty identifier is not a repeat: it spells "new specification".
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
    public static IEnumerable<string> Collect(IEnumerable<CreateAttributeSpecification>? specifications)
    {
        if (specifications == null)
        {
            yield break;
        }

        var specs = specifications.ToList();

        if (specs.Count(s => s.IsDescriptor) > 1)
        {
            yield return "Дескриптором можно отметить только один атрибут";
        }

        foreach (var spec in specs)
        {
            // An empty title is refused by the rule of the field (Apply); the
            // sentences about the rest of the attribute still need a name for it.
            var title = string.IsNullOrWhiteSpace(spec.Title) ? "Без названия" : spec.Title.Trim();

            if (spec.Type == AttributeSpecificationType.BbCode && spec.IsDescriptor)
            {
                yield return $"Атрибут '{title}' с разметкой не может быть дескриптором";
            }

            // Every reader of the game page sees the descriptor.
            if (spec.IsDescriptor && spec.IsHidden)
            {
                yield return $"Дескриптор '{title}' не может быть скрытым";
            }

            if (!IsListType(spec.Type))
            {
                continue;
            }

            var values = spec.Values?.ToList() ?? [];

            if (values.Count == 0)
            {
                yield return $"У списка '{title}' должно быть хотя бы одно значение";
                continue;
            }

            // Values are matched by text when read.
            var distinctCount = values.Select(v => v.Value?.Trim()).Distinct().Count();
            if (distinctCount != values.Count)
            {
                yield return $"В списке '{title}' значения не должны повторяться";
            }

            if (spec.Type == AttributeSpecificationType.TextNumberList &&
                values.Any(v => string.IsNullOrWhiteSpace(v.Value) || v.Modifier == null))
            {
                yield return $"В списке '{title}' у каждого варианта нужны и значение, и модификатор";
            }
        }
    }

    internal static bool IsListType(AttributeSpecificationType type) =>
        type is AttributeSpecificationType.TextList
            or AttributeSpecificationType.NumberList
            or AttributeSpecificationType.TextNumberList;
}
