using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class CreateBanValidatorShould : UnitTestBase
{
    private readonly CreateBanValidator validator;

    public CreateBanValidatorShould()
    {
        validator = new CreateBanValidator();
    }

    [Fact]
    public void PassForValidInputWithDurationHours()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid ban comment",
            DurationHours = 24
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassForValidInputWithExpiresUtc()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid ban comment",
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassForVoluntaryBan()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid ban comment",
            IsVoluntary = true
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenUsernameIsEmpty()
    {
        var input = new CreateBan
        {
            Username = "",
            Comment = "Valid comment",
            DurationHours = 24
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenCommentIsEmpty()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "",
            DurationHours = 24
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Comment)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenCommentExceedsMaxLength()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = new string('x', 2001),
            DurationHours = 24
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Comment)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenDurationHoursIsZero()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid comment",
            DurationHours = 0
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.DurationHours)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenDurationHoursIsNegative()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid comment",
            DurationHours = -1
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.DurationHours)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenExpiresUtcIsInPast()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid comment",
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.ExpiresUtc)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenNoDurationSpecifiedForNonVoluntaryBan()
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid comment",
            IsVoluntary = false
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Duration")
            .WithErrorMessage("Для бана укажите срок или дату окончания");
    }

    /// <summary>
    /// Two ban scopes exist, and a request naming anything else is refused. The
    /// type is [Flags] because a user's effective policy is a union of bans, so it
    /// accepts eight values where a ban's scope has two. The rest used to be
    /// coerced to FullBan in silence: a moderator sending 0 issued the strictest
    /// ban on the site and was answered as if he had asked for it.
    /// </summary>
    [Theory]
    [InlineData(AccessPolicy.NotSpecified)]
    [InlineData((AccessPolicy)2)]
    [InlineData(AccessPolicy.DemocraticBan | AccessPolicy.FullBan)]
    [InlineData((AccessPolicy)7)]
    public void RefuseAScopeThatIsNeitherOfTheTwo(AccessPolicy policy)
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid comment",
            DurationHours = 24,
            AccessRestrictionPolicy = policy
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.AccessRestrictionPolicy)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Theory]
    [InlineData(AccessPolicy.DemocraticBan)]
    [InlineData(AccessPolicy.FullBan)]
    public void PassForEitherBanScope(AccessPolicy policy)
    {
        var input = new CreateBan
        {
            Username = "testuser",
            Comment = "Valid comment",
            DurationHours = 24,
            AccessRestrictionPolicy = policy
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.AccessRestrictionPolicy);
    }
}
