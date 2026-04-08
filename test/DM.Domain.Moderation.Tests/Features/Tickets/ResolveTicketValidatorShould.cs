using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using FluentAssertions;
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
            Status = TicketStatus.Resolved,
            Answer = "Valid answer"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenStatusIsOpen()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Open,
            Answer = "Valid answer"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Status must be a resolution status (Resolved, Rejected, etc.)");
    }

    [Fact]
    public void FailWhenStatusIsInProgress()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.InProgress,
            Answer = "Valid answer"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Status must be a resolution status (Resolved, Rejected, etc.)");
    }

    [Fact]
    public void FailWhenAnswerIsEmpty()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Resolved,
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
            Status = TicketStatus.Resolved,
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
            Status = TicketStatus.Resolved,
            Answer = "Valid answer",
            IssueWarning = true,
            WarningPoints = 4,
            WarningText = "Valid warning"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.WarningPoints)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenWarningTextIsEmptyForWarning()
    {
        var input = new ResolveTicket
        {
            Status = TicketStatus.Resolved,
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
            Status = TicketStatus.Resolved,
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
            Status = TicketStatus.Resolved,
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
            Status = TicketStatus.Resolved,
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
            Status = TicketStatus.Resolved,
            Answer = "Valid answer",
            IssueBan = true,
            BanDurationHours = 48,
            BanComment = "Valid ban comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
