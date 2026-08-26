using System;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class UpdatePollValidatorShould : UnitTestBase
{
    private readonly UpdatePollValidator validator;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now;

    public UpdatePollValidatorShould()
    {
        now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);
        validator = new UpdatePollValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            Title = "Updated title"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenIdIsEmpty()
    {
        var input = new UpdatePoll
        {
            Id = Guid.Empty,
            Title = "Updated title"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            Title = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassWhenTitleIsNull()
    {
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            Title = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AllowAnEndDateInThePastBecauseAClosedPollIsStillEditable()
    {
        // Creation requires future dates; an update must not, or every running or
        // finished poll becomes uneditable - the edit form sends back the dates it
        // was given.
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            EndsUtc = now.AddDays(-1)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.EndsUtc);
    }

    [Fact]
    public void FailWhenThePollWouldEndBeforeItStarts()
    {
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            StartsUtc = now,
            EndsUtc = now.AddHours(-1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndsUtc)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassWhenEndDateIsNull()
    {
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            EndsUtc = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
