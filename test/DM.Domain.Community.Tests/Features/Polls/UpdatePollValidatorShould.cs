using System;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class UpdatePollValidatorShould : UnitTestBase
{
    private readonly UpdatePollValidator validator;
    private readonly Mock<IDateTimeProvider> dateTimeProvider;
    private readonly DateTimeOffset now;

    public UpdatePollValidatorShould()
    {
        now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.Now).Returns(now);
        validator = new UpdatePollValidator(dateTimeProvider.Object);
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
    public void FailWhenEndDateIsInPast()
    {
        var input = new UpdatePoll
        {
            Id = Guid.NewGuid(),
            EndsUtc = now.AddDays(-1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndsUtc)
            .WithErrorMessage(ValidationError.Short);
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
