using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.GlobalChatEvents;

public class CreateGlobalChatEventValidatorShould
{
    private readonly CreateGlobalChatEventValidator validator;
    private readonly DateTimeOffset now = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public CreateGlobalChatEventValidatorShould()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);
        validator = new CreateGlobalChatEventValidator(dateTimeProvider);
    }

    private CreateGlobalChatEvent ValidEvent(Action<CreateGlobalChatEvent>? change = null)
    {
        var input = new CreateGlobalChatEvent { Title = "Valid title", StartsUtc = now.AddHours(1) };
        change?.Invoke(input);
        return input;
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = ValidEvent(e =>
        {
            e.Description = "Valid description";
            e.Duration = TimeSpan.FromHours(2);
        });

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTitleIsEmpty()
    {
        var result = validator.TestValidate(ValidEvent(e => e.Title = ""));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTitleExceedsMaxLength()
    {
        var result = validator.TestValidate(
            ValidEvent(e => e.Title = new string('x', GlobalChatEventFieldLimits.TitleMaxLength + 1)));
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenDescriptionExceedsMaxLength()
    {
        var result = validator.TestValidate(
            ValidEvent(e => e.Description = new string('x', GlobalChatEventFieldLimits.DescriptionMaxLength + 1)));
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenStartsAtIsInPast()
    {
        var result = validator.TestValidate(ValidEvent(e => e.StartsUtc = now.AddHours(-1)));
        result.ShouldHaveValidationErrorFor(x => x.StartsUtc)
            .WithErrorMessage(ValidationError.MustBeFuture);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FailWhenDurationIsNotPositive(int hours)
    {
        var result = validator.TestValidate(ValidEvent(e => e.Duration = TimeSpan.FromHours(hours)));
        result.ShouldHaveValidationErrorFor(x => x.Duration)
            .WithErrorMessage(ValidationError.MustBePositive);
    }

    [Fact]
    public void PassWhenDurationIsNull()
    {
        var result = validator.TestValidate(ValidEvent(e => e.Duration = null));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
