using DM.Domain.Game.Features.Games;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GamesQueryValidatorShould : UnitTestBase
{
    private readonly GamesQueryValidator validator = new();

    [Fact]
    public void PassForEmptyQuery()
    {
        var result = validator.TestValidate(new GamesQuery());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassForFilledQuery()
    {
        var input = new GamesQuery
        {
            Search = "dragons",
            OwnerUsernames = ["master"],
            PlayerUsername = "player",
            SortBy = "popularity",
            SortOrder = "asc",
            RequiredTags = [1],
            OptionalTags = [2],
            ExcludedTags = [3]
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenSearchExceedsMaxLength()
    {
        var input = new GamesQuery { Search = new string('a', 201) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(q => q.Search)
            .WithErrorMessage("Search query must not exceed 200 characters");
    }

    [Fact]
    public void FailWhenOwnerUsernameIsEmpty()
    {
        var input = new GamesQuery { OwnerUsernames = [""] };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("OwnerUsernames[0]")
            .WithErrorMessage("Username cannot be empty");
    }

    [Fact]
    public void FailWhenOwnerUsernameExceedsMaxLength()
    {
        var input = new GamesQuery { OwnerUsernames = [new string('a', 21)] };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("OwnerUsernames[0]")
            .WithErrorMessage("Username must not exceed 20 characters");
    }

    [Fact]
    public void FailWhenPlayerUsernameExceedsMaxLength()
    {
        var input = new GamesQuery { PlayerUsername = new string('a', 21) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(q => q.PlayerUsername)
            .WithErrorMessage("Username must not exceed 20 characters");
    }

    [Fact]
    public void FailWhenSortByIsNotAllowed()
    {
        var input = new GamesQuery { SortBy = "random" };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(q => q.SortBy);
    }

    [Fact]
    public void AcceptSortByCaseInsensitively()
    {
        var input = new GamesQuery { SortBy = "Popularity" };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenSortOrderIsNotAllowed()
    {
        var input = new GamesQuery { SortOrder = "sideways" };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(q => q.SortOrder)
            .WithErrorMessage("SortOrder must be 'asc' or 'desc'");
    }

    [Fact]
    public void AcceptSortOrderCaseInsensitively()
    {
        var input = new GamesQuery { SortOrder = "DESC" };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenRequiredTagIsNotPositive()
    {
        var input = new GamesQuery { RequiredTags = [0] };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("RequiredTags[0]")
            .WithErrorMessage("Tag IDs must be positive integers");
    }

    [Fact]
    public void FailWhenOptionalTagIsNotPositive()
    {
        var input = new GamesQuery { OptionalTags = [-1] };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("OptionalTags[0]")
            .WithErrorMessage("Tag IDs must be positive integers");
    }

    [Fact]
    public void FailWhenExcludedTagIsNotPositive()
    {
        var input = new GamesQuery { ExcludedTags = [0] };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("ExcludedTags[0]")
            .WithErrorMessage("Tag IDs must be positive integers");
    }
}
