using System;
using System.Collections.Generic;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class CreatePollValidatorShould : UnitTestBase
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

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreatePoll
        {
            Title = "Valid poll title",
            StartsUtc = now,
            EndsUtc = now.AddDays(7),
            Options = new List<string> { "Option 1", "Option 2" }
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreatePoll
        {
            Title = "",
            StartsUtc = now,
            EndsUtc = now.AddDays(7),
            Options = new List<string> { "Option 1", "Option 2" }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenEndDateIsTooSoon()
    {
        var input = new CreatePoll
        {
            Title = "Valid title",
            StartsUtc = now,
            EndsUtc = now.AddHours(12),
            Options = new List<string> { "Option 1", "Option 2" }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndsUtc)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public void FailWhenEndDateIsTooFar()
    {
        var input = new CreatePoll
        {
            Title = "Valid title",
            StartsUtc = now,
            EndsUtc = now.AddDays(366),
            Options = new List<string> { "Option 1", "Option 2" }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndsUtc)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenOptionsAreEmpty()
    {
        var input = new CreatePoll
        {
            Title = "Valid title",
            StartsUtc = now,
            EndsUtc = now.AddDays(7),
            Options = new List<string>()
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Options)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenOptionIsEmpty()
    {
        var input = new CreatePoll
        {
            Title = "Valid title",
            StartsUtc = now,
            EndsUtc = now.AddDays(7),
            Options = new List<string> { "Option 1", "" }
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Options[1]")
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassWithMinimalValidDuration()
    {
        var input = new CreatePoll
        {
            Title = "Valid title",
            StartsUtc = now,
            EndsUtc = now.AddDays(1).AddMinutes(1),
            Options = new List<string> { "Option 1", "Option 2" }
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
