using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Web.API.Features.Community.WebsiteTestimonials;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Community;

// The request takes the length the domain takes, and its refusal names the bounds themselves: the
// form shows the sentence as it comes.
public class WebsiteTestimonialLengthShould
{
    [Fact]
    public void TakeTheLongestTextTheDomainTakes() =>
        Refusals(new string('а', WebsiteTestimonialFieldLimits.TextMaxLength)).Should().BeEmpty();

    [Fact]
    public void RefuseALongerTextNamingTheBounds() =>
        Refusals(new string('а', WebsiteTestimonialFieldLimits.TextMaxLength + 1)).Should().Equal(
            $"Отзыв от 1 до {WebsiteTestimonialFieldLimits.TextMaxLength} символов");

    [Fact]
    public void RefuseALongerEditInTheSameWords() =>
        Validate(new UpdateWebsiteTestimonialRequest
        {
            Text = new string('а', WebsiteTestimonialFieldLimits.TextMaxLength + 1)
        })
            .Should().Equal($"Отзыв от 1 до {WebsiteTestimonialFieldLimits.TextMaxLength} символов");

    private static IEnumerable<string?> Refusals(string text) =>
        Validate(new CreateWebsiteTestimonialRequest { AuthorUsername = "Reader", Text = text });

    private static IEnumerable<string?> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results.Select(result => result.ErrorMessage);
    }
}
