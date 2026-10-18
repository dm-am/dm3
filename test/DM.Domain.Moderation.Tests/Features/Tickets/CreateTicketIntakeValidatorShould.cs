using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class CreateTicketIntakeValidatorShould : UnitTestBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IBbCodeNestingLimit _nestingLimit;
    private readonly CreateTicketIntakeValidator _validator;

    public CreateTicketIntakeValidatorShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _nestingLimit = Mock<IBbCodeNestingLimit>();
        _nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        _validator = new CreateTicketIntakeValidator(_identityProvider, _nestingLimit);
    }

    private void SetGuest() =>
        _identityProvider.Current.Returns(Identity.Guest());

    private void SetAuthenticated() =>
        _identityProvider.Current.Returns(Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token"));

    private static CreateTicketIntake ValidIntake() => new()
    {
        Subtype = TicketSubtype.Bug,
        Text = "Body text"
    };

    /// <summary>
    /// The text of a ticket and every comment of its conversation share one
    /// length (TicketTextLimits).
    /// </summary>
    [Fact]
    public void TakeATextAsLongAsTheTicketLimit()
    {
        SetAuthenticated();
        var longest = ValidIntake();
        longest.Text = new string('а', TicketTextLimits.MaxLength);
        var tooLong = ValidIntake();
        tooLong.Text = new string('а', 10001);

        _validator.TestValidate(longest).ShouldNotHaveValidationErrorFor(x => x.Text);
        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenGuestOmitsContactEmail()
    {
        // A guest submission with no contact email must be rejected — the
        // moderation reply and the tracking flow have nowhere to land.
        SetGuest();
        var input = ValidIntake();
        input.Contact = null;

        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Contact)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenGuestContactIsNotAnEmail()
    {
        SetGuest();
        var input = ValidIntake();
        input.Contact = "not-an-email";

        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Contact)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassWhenGuestProvidesValidEmail()
    {
        SetGuest();
        var input = ValidIntake();
        input.Contact = "guest@example.com";

        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenAuthenticatedUserOmitsContact()
    {
        // Authenticated authors are reachable by identity, so contact stays
        // optional for them.
        SetAuthenticated();
        var input = ValidIntake();
        input.Contact = null;

        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(TicketSubtype.AccountRestoration)]
    [InlineData(TicketSubtype.PersonalData)]
    [InlineData(TicketSubtype.AgreementQuestion)]
    [InlineData(TicketSubtype.SeniorModeratorDecisionComplaint)]
    public void PassWhenGuestAsksForAnAccountOrALegalPageReason(TicketSubtype subtype)
    {
        // Bringing an account back carries no extra gate: a person locked out of
        // it has no other way to ask. Neither do the questions the legal pages
        // send a reader with, who need not have an account at all, nor the
        // complaint about a senior moderator's decision, which is the way out
        // for an account a full ban keeps from signing in.
        SetGuest();
        var input = ValidIntake();
        input.Subtype = subtype;
        input.Contact = "guest@example.com";

        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PassWithoutSubject(string? subject)
    {
        // The support and complaint forms do not ask for a subject: the reason
        // and the text say it, and the ticket screens name a ticket by its
        // reason when the subject is empty.
        SetGuest();
        var input = ValidIntake();
        input.Subject = subject;
        input.Contact = "guest@example.com";

        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefuseSuggestionFromGuestAndMember()
    {
        // Suggestions are discussed on the forum; the category is kept only for
        // the queues that filter by it, and nothing may file one any more.
        var input = ValidIntake();
        input.Subtype = TicketSubtype.SiteImprovementSuggestion;
        input.Contact = "guest@example.com";

        SetGuest();
        _validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.Subtype)
            .WithErrorMessage(ValidationError.Invalid);

        SetAuthenticated();
        _validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.Subtype)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// A complaint about a person and a request to remove an account are filed
    /// from an account and from nowhere else.
    /// </summary>
    /// <remarks>
    /// The complaint ends in a warning or a ban and goes to a reporter the
    /// moderator can write back to, as on DM2; the removal is honoured for the
    /// owner, and a guest's request cannot say whose account it names.
    /// </remarks>
    [Theory]
    [InlineData(TicketSubtype.UserComplaint)]
    [InlineData(TicketSubtype.AccountRemoval)]
    public void RefuseAGuestTheReasonsFiledFromAnAccount(TicketSubtype subtype)
    {
        var input = ValidIntake();
        input.Subtype = subtype;
        input.Contact = "guest@example.com";
        input.TargetUsername = "Violator";
        input.ViolationUrl = "https://dm.am/forum/flood/1";

        SetGuest();
        _validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.Subtype)
            .WithErrorMessage(CreateTicketIntakeValidator.SignInForThisReason);

        SetAuthenticated();
        _validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// A complaint about a person names the person and the place.
    /// </summary>
    [Fact]
    public void RequireTheViolatorAndTheLinkOfAComplaintAboutAPerson()
    {
        SetAuthenticated();
        var input = ValidIntake();
        input.Subtype = TicketSubtype.UserComplaint;

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(x => x.TargetUsername)
            .WithErrorMessage(ValidationError.Empty);
        result.ShouldHaveValidationErrorFor(x => x.ViolationUrl)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Theory]
    [InlineData("forum/flood/1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://dm.am/file")]
    public void RefuseALinkOfAComplaintThatLeadsNowhere(string link)
    {
        SetAuthenticated();
        var input = ValidIntake();
        input.Subtype = TicketSubtype.UserComplaint;
        input.TargetUsername = "Violator";
        input.ViolationUrl = link;

        _validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.ViolationUrl)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// A complaint about a decision keeps its link optional: the moderator
    /// finds the decision by the name of whoever complains.
    /// </summary>
    [Theory]
    [InlineData(TicketSubtype.ModeratorDecisionComplaint)]
    [InlineData(TicketSubtype.SeniorModeratorDecisionComplaint)]
    public void LeaveTheLinkOfAComplaintAboutADecisionOptional(TicketSubtype subtype)
    {
        SetAuthenticated();
        var input = ValidIntake();
        input.Subtype = subtype;

        _validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        // Stored, a text nested past the renderer's depth shows moderation an
        // empty ticket; refused here, the author can still fix it.
        SetAuthenticated();
        _nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var input = ValidIntake();

        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void RefuseHidingMarkup()
    {
        // A ticket renders with the tag set of profile texts, where a hiding
        // block is shown to nobody: the line meant for moderation would reach
        // none of its readers.
        SetAuthenticated();
        var input = ValidIntake();
        input.Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после";

        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(PrivateBlockMarkup.DescribeSurfaceRefusal(input.Text));
    }

    [Fact]
    public void AcceptHidingMarkupShownAsAnExample()
    {
        // A closed [noparse] turns its content into text, so a ticket may quote
        // the tag while asking about it.
        SetAuthenticated();
        var input = ValidIntake();
        input.Text = "Не работает [noparse][private=\"Чак\"]текст[/private][/noparse] в посте";

        var result = _validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        SetAuthenticated();
        var input = ValidIntake();
        input.Text = "";

        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }
}
