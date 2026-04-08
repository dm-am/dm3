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
            .WithMessage("Search query must not exceed 200 characters");

        // Username policy: 2-20 characters (see docs/conventions/USERNAME_POLICY.md)
        // Format validation not needed here - invalid usernames simply won't match any user
        RuleForEach(q => q.OwnerUsernames)
            .NotEmpty()
            .WithMessage("Username cannot be empty")
            .MaximumLength(20)
            .When(q => q.OwnerUsernames != null)
            .WithMessage("Username must not exceed 20 characters");

        RuleFor(q => q.PlayerUsername)
            .MaximumLength(20)
            .WithMessage("Username must not exceed 20 characters");

        RuleFor(q => q.SortBy)
            .Must(BeValidSortField)
            .When(q => !string.IsNullOrEmpty(q.SortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", AllowedSortFields)}");

        RuleFor(q => q.SortOrder)
            .Must(BeValidSortOrder)
            .When(q => !string.IsNullOrEmpty(q.SortOrder))
            .WithMessage("SortOrder must be 'asc' or 'desc'");

        RuleForEach(q => q.RequiredTags)
            .GreaterThan(0)
            .When(q => q.RequiredTags != null)
            .WithMessage("Tag IDs must be positive integers");

        RuleForEach(q => q.OptionalTags)
            .GreaterThan(0)
            .When(q => q.OptionalTags != null)
            .WithMessage("Tag IDs must be positive integers");

        RuleForEach(q => q.ExcludedTags)
            .GreaterThan(0)
            .When(q => q.ExcludedTags != null)
            .WithMessage("Tag IDs must be positive integers");
    }

    private static bool BeValidSortField(string? sortBy) =>
        sortBy != null && AllowedSortFields.Contains(sortBy.ToLowerInvariant());

    private static bool BeValidSortOrder(string? sortOrder) =>
        sortOrder != null && AllowedSortOrders.Contains(sortOrder.ToLowerInvariant());
}
