using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Web.API.Features.Game.Reviews;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

// How long a review may be is the domain's rule (GameReviewFieldLimits), on both paths, counted the
// way the service stores the text; checked as MVC checks the request.
public class GameReviewRequestShould
{
    [Theory]
    [InlineData(3)]
    [InlineData(6000)]
    public void LeaveTheLengthOfACreatedReviewToTheDomain(int length) =>
        Refusals(new CreateGameReviewRequest { Text = new string('a', length) }).Should().BeEmpty();

    [Theory]
    [InlineData(3)]
    [InlineData(6000)]
    public void LeaveTheLengthOfAnEditedReviewToTheDomain(int length) =>
        Refusals(new UpdateGameReviewRequest { Text = new string('a', length) }).Should().BeEmpty();

    [Fact]
    public void StillRequireTheTextOfACreatedReview() =>
        Refusals(new CreateGameReviewRequest { Text = null! }).Should().NotBeEmpty();

    private static List<ValidationResult> Refusals(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
