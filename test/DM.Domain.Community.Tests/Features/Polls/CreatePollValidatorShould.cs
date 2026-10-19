using System;
using System.Collections.Generic;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class CreatePollValidatorShould
{
    private readonly CreatePollValidator validator;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now;

    public CreatePollValidatorShould()
    {
        now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);
        validator = new CreatePollValidator(dateTimeProvider);
    }

    private CreatePoll ValidPoll(
        string title = "Valid poll title", DateTimeOffset? endsUtc = null, List<string>? options = null) => new()
        {
            Title = title,
            StartsUtc = now,
            EndsUtc = endsUtc ?? now.AddDays(7),
            Options = options ?? ["Option 1", "Option 2"]
        };

    [Fact]
    public void PassForValidInput()
    {
        var input = ValidPoll();

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = ValidPoll(title: "");

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenEndDateIsTooSoon()
    {
        var input = ValidPoll(endsUtc: now.AddHours(12));

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndsUtc)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public void FailWhenEndDateIsTooFar()
    {
        var input = ValidPoll(endsUtc: now.AddDays(366));

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndsUtc)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenOptionsAreEmpty()
    {
        var input = ValidPoll(options: []);

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Options)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenOptionIsEmpty()
    {
        var input = ValidPoll(options: ["Option 1", ""]);

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Options[1]")
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassForAStartLessThanAMinuteBehindTheClock()
    {
        var input = ValidPoll();
        input.StartsUtc = now.AddSeconds(-30);

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    public void FailForAStartAMinuteOrMoreBehindTheClock(int secondsBehind)
    {
        var input = ValidPoll();
        input.StartsUtc = now.AddSeconds(-secondsBehind);

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.StartsUtc)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public void PassWithMinimalValidDuration()
    {
        var input = ValidPoll(endsUtc: now.AddDays(1).AddMinutes(1));

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
