using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

/// <summary>
/// An answer of a ticket is held to the text of a ticket: as long, refused for
/// the same hiding markup; and it carries one of the statuses moderation answers
/// with, the text staying empty only for a status that ends the ticket.
/// </summary>
public class CreateTicketCommentValidatorShould : UnitTestBase
{
    private readonly IBbCodeNestingLimit _nestingLimit;
    private readonly CreateTicketCommentValidator _validator;

    public CreateTicketCommentValidatorShould()
    {
        _nestingLimit = Mock<IBbCodeNestingLimit>();
        _nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        _validator = new CreateTicketCommentValidator(_nestingLimit);
    }

    /// <summary>
    /// Ten thousand characters, as the text of the ticket itself, and not the fifty
    /// thousand of a comment of a topic: the whole conversation comes back with the
    /// ticket in one answer.
    /// </summary>
    [Fact]
    public void TakeATextAsLongAsTheTextOfATicket()
    {
        var longest = new CreateTicketComment { Text = new string('а', 10000) };
        var tooLong = new CreateTicketComment { Text = new string('а', 10001) };

        _validator.TestValidate(longest).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    /// <summary>
    /// An answer that passes the turn to the author, or is the author's own, says
    /// something.
    /// </summary>
    [Theory]
    [InlineData(null, "")]
    [InlineData(null, "   ")]
    [InlineData(TicketStatus.WaitingForUser, "")]
    public void RefuseAnEmptyText(TicketStatus? status, string text)
    {
        _validator.TestValidate(new CreateTicketComment { Text = text, Status = status })
            .ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    /// <summary>
    /// Closing a ticket or marking it as spam is an answer by itself: the text may
    /// stay empty, and the status is all the answer changes.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Spam)]
    public void TakeAnEmptyTextWithAStatusThatEndsTheTicket(TicketStatus status)
    {
        _validator.TestValidate(new CreateTicketComment { Text = "", Status = status })
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
        var result = _validator.TestValidate(new CreateTicketComment { Text = "Ответ", Status = status });

        if (taken)
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
        else
        {
            result.ShouldHaveValidationErrorFor(c => c.Status).WithErrorMessage(ValidationError.Invalid);
        }
    }

    /// <summary>
    /// The text of a closing answer, when there is one, is held to what every text
    /// of the conversation is.
    /// </summary>
    [Fact]
    public void HoldTheTextOfAClosingAnswerToTheRulesOfEveryAnswer()
    {
        var tooLong = new CreateTicketComment { Text = new string('а', 10001), Status = TicketStatus.Closed };
        var hiding = new CreateTicketComment { Text = "[private=\"Чак\"]секрет[/private]", Status = TicketStatus.Spam };

        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Long);
        _validator.TestValidate(hiding).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(PrivateBlockMarkup.DescribeSurfaceRefusal(hiding.Text));
    }

    /// <summary>
    /// The conversation renders with the tag set of comments, where a hiding block
    /// hides nothing.
    /// </summary>
    [Fact]
    public void RefuseHidingMarkup()
    {
        var input = new CreateTicketComment { Text = "до [private=\"Чак\"]секрет[/private] после" };

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(PrivateBlockMarkup.DescribeSurfaceRefusal(input.Text));
    }

    [Fact]
    public void RefuseATextTheRendererWouldRefuse()
    {
        _nestingLimit.IsWithinLimit("[b][b]глубоко[/b][/b]").Returns(false);

        _validator.TestValidate(new CreateTicketComment { Text = "[b][b]глубоко[/b][/b]" })
            .ShouldHaveValidationErrorFor(c => c.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
