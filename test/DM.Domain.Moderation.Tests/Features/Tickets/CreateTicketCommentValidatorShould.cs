using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class CreateTicketCommentValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit nestingLimit;
    private readonly CreateTicketCommentValidator validator;

    public CreateTicketCommentValidatorShould()
    {
        nestingLimit = Mock<IBbCodeNestingLimit>();
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new CreateTicketCommentValidator(nestingLimit);
    }

    /// <summary>
    /// Ten thousand characters, as the text of the ticket itself, and not the fifty
    /// thousand of a comment of a topic: the whole conversation comes back with the
    /// ticket in one answer.
    /// </summary>
    [Fact]
    public void TakeATextAsLongAsTheTextOfATicket()
    {
        var longest = new CreateTicketComment { Text = new string('а', TicketTextLimits.MaxLength) };
        var tooLong = new CreateTicketComment { Text = new string('а', TicketTextLimits.MaxLength + 1) };

        validator.TestValidate(longest).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(null, "   ")]
    [InlineData(TicketStatus.WaitingForUser, "")]
    public void RefuseAnEmptyText(TicketStatus? status, string text)
    {
        validator.TestValidate(new CreateTicketComment { Text = text, Status = status })
            .ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Spam)]
    public void TakeAnEmptyTextWithAStatusThatEndsTheTicket(TicketStatus status)
    {
        validator.TestValidate(new CreateTicketComment { Text = "", Status = status })
            .ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Moderation answers with one of three statuses; waiting for moderation is the
    /// turn the author's answer gives, never a choice.
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(TicketStatus.WaitingForUser, true)]
    [InlineData(TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Spam, true)]
    [InlineData(TicketStatus.WaitingForModeration, false)]
    [InlineData((TicketStatus)42, false)]
    public void TakeTheStatusesModerationAnswersWith(TicketStatus? status, bool taken)
    {
        var result = validator.TestValidate(new CreateTicketComment { Text = "Ответ", Status = status });

        if (taken)
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
        else
        {
            result.ShouldHaveValidationErrorFor(c => c.Status).WithErrorMessage(ValidationError.Invalid);
        }
    }

    [Fact]
    public void HoldTheTextOfAClosingAnswerToTheRulesOfEveryAnswer()
    {
        var tooLong = new CreateTicketComment { Text = new string('а', TicketTextLimits.MaxLength + 1), Status = TicketStatus.Closed };
        var hiding = new CreateTicketComment { Text = "[private=\"Чак\"]секрет[/private]", Status = TicketStatus.Spam };

        validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Long);
        validator.TestValidate(hiding).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(PrivateBlockMarkup.DescribeSurfaceRefusal(hiding.Text));
    }

    // The conversation renders with the tag set of comments, where a hiding block hides nothing.
    [Fact]
    public void RefuseHidingMarkup()
    {
        var input = new CreateTicketComment { Text = "до [private=\"Чак\"]секрет[/private] после" };

        validator.TestValidate(input).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(PrivateBlockMarkup.DescribeSurfaceRefusal(input.Text));
    }

    [Fact]
    public void RefuseATextTheRendererWouldRefuse()
    {
        nestingLimit.IsWithinLimit("[b][b]глубоко[/b][/b]").Returns(false);

        validator.TestValidate(new CreateTicketComment { Text = "[b][b]глубоко[/b][/b]" })
            .ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
