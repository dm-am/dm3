using System.Linq;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Validator for games query parameters
/// </summary>
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

    public GamesQueryValidator()
    {
        RuleFor(q => q.Search)
            .MaximumLength(200)
            .WithMessage("Поисковый запрос не длиннее 200 символов");

        // Username policy: 2-20 characters (see docs/conventions/USERNAME_POLICY.md)
        // Format validation not needed here - invalid usernames simply won't match any user
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

        RuleForEach(q => q.RequiredTags)
            .GreaterThan(0)
            .When(q => q.RequiredTags != null)
            .WithMessage("Метка указана неверно");

        RuleForEach(q => q.OptionalTags)
            .GreaterThan(0)
            .When(q => q.OptionalTags != null)
            .WithMessage("Метка указана неверно");

        RuleForEach(q => q.ExcludedTags)
            .GreaterThan(0)
            .When(q => q.ExcludedTags != null)
            .WithMessage("Метка указана неверно");
    }

    private static bool BeValidSortField(string? sortBy) =>
        sortBy != null && AllowedSortFields.Contains(sortBy.ToLowerInvariant());

    private static bool BeValidSortOrder(string? sortOrder) =>
        sortOrder != null && AllowedSortOrders.Contains(sortOrder.ToLowerInvariant());
}
