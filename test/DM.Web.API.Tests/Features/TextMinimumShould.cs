using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Configuration;
using DM.Web.API.Features.Account.Credentials;
using DM.Web.API.Features.Community.Endorsements;
using DM.Web.API.Features.Community.Polls;
using DM.Web.API.Features.Community.WebsiteTestimonials;
using DM.Web.API.Features.Forum.Topics;
using DM.Web.API.Features.Game.Reviews;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features;

/// <summary>
/// A text of one character passes every request contract: a shortest length past
/// the empty text protects nothing. Checked as MVC checks the request.
/// </summary>
public class TextMinimumShould
{
    private const string OneCharacter = "Я";

    public static TheoryData<object> Requests => new()
    {
        new CreateGameReviewRequest { Text = OneCharacter },
        new UpdateGameReviewRequest { Text = OneCharacter },
        new CreateUserEndorsementRequest { Text = OneCharacter },
        new UpdateUserEndorsementRequest { Text = OneCharacter },
        new CreateWebsiteTestimonialRequest { AuthorUsername = "Reader", Text = OneCharacter },
        new UpdateWebsiteTestimonialRequest { Text = OneCharacter },
        new CreatePollRequest
        {
            Title = OneCharacter,
            StartsUtc = DateTimeOffset.UtcNow,
            EndsUtc = DateTimeOffset.UtcNow.AddDays(7),
            Options = ["Да", "Нет"]
        },
        new UpdatePollRequest { Title = OneCharacter },
        new CreateTopicRequest { Title = OneCharacter, Text = OneCharacter },
        new UsernameChangeCreateRequest { Reason = OneCharacter },
    };

    [Theory]
    [MemberData(nameof(Requests))]
    public void PassASingleCharacter(object request) => Refusals(request).Should().BeEmpty();

    /// <summary>
    /// The domain holds the length of a recommendation and of a reason, on both paths of the first.
    /// </summary>
    [Fact]
    public void LeaveTheLongestTextToTheDomain()
    {
        var past = new string('a', 6000);

        Refusals(new CreateUserEndorsementRequest { Text = past }).Should().BeEmpty();
        Refusals(new UpdateUserEndorsementRequest { Text = past }).Should().BeEmpty();
        Refusals(new UsernameChangeCreateRequest { Reason = past }).Should().BeEmpty();
    }

    /// <summary>
    /// The number in the message is the one the request is measured against.
    /// </summary>
    [Fact]
    public void RefuseATopicTitlePastThePolicyInItsNumber() =>
        Refusals(new CreateTopicRequest { Title = new string('a', TopicPolicy.TitleMaxLength + 1), Text = OneCharacter })
            .Should().ContainSingle().Which.ErrorMessage
            .Should().Be($"Заголовок должен быть от 1 до {TopicPolicy.TitleMaxLength} символов");

    private static List<ValidationResult> Refusals(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
