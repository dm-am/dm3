using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.GlobalChatEvents;

public class CreateGlobalChatEventValidatorShould : UnitTestBase
{
    private readonly CreateGlobalChatEventValidator validator;
    private readonly Mock<IDateTimeProvider> dateTimeProvider;
    private readonly DateTimeOffset now;

    public CreateGlobalChatEventValidatorShould()
    {
        now = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.Now).Returns(now);
        validator = new CreateGlobalChatEventValidator(dateTimeProvider.Object);
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "Valid event title",
            Description = "Valid description",
            StartsUtc = now.AddHours(1),
            Duration = TimeSpan.FromHours(2)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "",
            StartsUtc = now.AddHours(1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = new string('x', 201),
            StartsUtc = now.AddHours(1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenDescriptionExceedsMaxLength()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "Valid title",
            Description = new string('x', 10001),
            StartsUtc = now.AddHours(1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenStartsAtIsInPast()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "Valid title",
            StartsUtc = now.AddHours(-1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.StartsUtc)
            .WithErrorMessage(ValidationError.MustBeFuture);
    }

    [Fact]
    public void FailWhenDurationIsZero()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "Valid title",
            StartsUtc = now.AddHours(1),
            Duration = TimeSpan.Zero
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Duration)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void FailWhenDurationIsNegative()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "Valid title",
            StartsUtc = now.AddHours(1),
            Duration = TimeSpan.FromHours(-1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Duration)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void PassWhenDurationIsNull()
    {
        var input = new CreateGlobalChatEvent
        {
            Title = "Valid title",
            StartsUtc = now.AddHours(1),
            Duration = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
