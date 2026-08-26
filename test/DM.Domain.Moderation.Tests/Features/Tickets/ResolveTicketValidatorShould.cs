using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class ResolveTicketValidatorShould : UnitTestBase
{
    private readonly ResolveTicketValidator validator;

    public ResolveTicketValidatorShould()
    {
        validator = new ResolveTicketValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenStatusIsWaitingForModeration()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.WaitingForModeration,
            Answer = "Valid answer"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Обращение можно закрыть или пометить спамом");
    }

    [Fact]
    public void FailWhenStatusIsWaitingForUser()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.WaitingForUser,
            Answer = "Valid answer"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Обращение можно закрыть или пометить спамом");
    }

    [Fact]
    public void FailWhenAnswerIsEmpty()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Answer)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenAnswerExceedsMaxLength()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = new string('x', 2001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Answer)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenWarningPointsAreInvalid()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueWarning = true,
            WarningPoints = 1000,
            WarningText = "Valid warning"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.WarningPoints)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassForZeroWarningPoints()
    {
        // 0 = verbal warning: recorded without points, within the 0-6 range.
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueWarning = true,
            WarningPoints = 0,
            WarningText = "Valid warning"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.WarningPoints);
    }

    [Fact]
    public void FailWhenBanDurationExceedsUpperBound()
    {
        // A ticket-issued ban is time-boxed; an absurd duration is rejected
        // (also guards against a DateTimeOffset overflow when added to "now").
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueBan = true,
            BanDurationHours = int.MaxValue,
            BanComment = "Valid ban comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.BanDurationHours)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenWarningTextIsEmptyForWarning()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueWarning = true,
            WarningPoints = 2,
            WarningText = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.WarningText)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenBanDurationIsInvalidForBan()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueBan = true,
            BanDurationHours = -1,
            BanComment = "Valid ban comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.BanDurationHours)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenBanCommentIsEmptyForBan()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueBan = true,
            BanDurationHours = 24,
            BanComment = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.BanComment)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void PassWithValidWarning()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueWarning = true,
            WarningPoints = 2,
            WarningText = "Valid warning text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWithValidBan()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            Answer = "Valid answer",
            IssueBan = true,
            BanDurationHours = 48,
            BanComment = "Valid ban comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
