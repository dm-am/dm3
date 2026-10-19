using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

internal class GamesQueryValidator : AbstractValidator<GamesQuery>
{
    private static readonly string[] AllowedSortFields =
    [
        "created",
        "recruitmentstarted",
        "title",
        "popularity",
        "status",
        "availableslots",
        "activated",
        "closed"
    ];

    private static readonly string[] AllowedSortOrders = ["asc", "desc"];

    /// <summary>
    /// The three lists of tags a query filters by, each held to the same rule.
    /// </summary>
    /// <remarks>An absent list has nothing to check.</remarks>
    private static readonly Expression<Func<GamesQuery, IEnumerable<int>>>[] TagFilters =
    [
        q => q.RequiredTags!,
        q => q.OptionalTags!,
        q => q.ExcludedTags!
    ];

    public GamesQueryValidator()
    {
        RuleFor(q => q.Search)
            .MaximumLength(200)
            .WithMessage("Поисковый запрос не длиннее 200 символов");

        // No format check: an invalid name matches nobody.
        RuleForEach(q => q.OwnerUsernames)
            .NotEmpty()
            .WithMessage("Введите имя пользователя")
            .MaximumLength(20)
            .When(q => q.OwnerUsernames != null)
            .WithMessage("Имя пользователя не длиннее 20 символов");

        RuleFor(q => q.PlayerUsername)
            .MaximumLength(20)
            .WithMessage("Имя пользователя не длиннее 20 символов");

        RuleFor(q => q.SortBy)
            .Must(BeValidSortField)
            .When(q => !string.IsNullOrEmpty(q.SortBy))
            .WithMessage("Недопустимое поле сортировки");

        RuleFor(q => q.SortOrder)
            .Must(BeValidSortOrder)
            .When(q => !string.IsNullOrEmpty(q.SortOrder))
            .WithMessage("Порядок сортировки: asc или desc");

        foreach (var tags in TagFilters)
        {
            RuleForEach(tags)
                .GreaterThan(0)
                .WithMessage("Тег указан неверно");
        }
    }

    private static bool BeValidSortField(string? sortBy) =>
        AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase);

    private static bool BeValidSortOrder(string? sortOrder) =>
        AllowedSortOrders.Contains(sortOrder, StringComparer.OrdinalIgnoreCase);
}
