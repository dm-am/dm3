using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class CreateTicketIntakeValidatorShould : UnitTestBase
{
    private readonly IIdentityProvider identityProvider;
    private readonly IBbCodeNestingLimit nestingLimit;
    private readonly CreateTicketIntakeValidator validator;

    public CreateTicketIntakeValidatorShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        nestingLimit = Mock<IBbCodeNestingLimit>();
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(true);
        validator = new CreateTicketIntakeValidator(identityProvider, nestingLimit);
    }

    private void SetGuest() =>
        identityProvider.Current.Returns(Identity.Guest());

    private void SetAuthenticated() =>
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "User"));

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
        tooLong.Text = new string('а', TicketTextLimits.MaxLength + 1);

        validator.TestValidate(longest).ShouldNotHaveValidationErrorFor(x => x.Text);
        validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenGuestOmitsContactEmail()
    {
        // The reply to a guest has nowhere else to land.
        SetGuest();
        var input = ValidIntake();
        input.Contact = null;

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Contact)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenGuestContactIsNotAnEmail()
    {
        SetGuest();
        var input = ValidIntake();
        input.Contact = "not-an-email";

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Contact)
            .WithErrorMessage(RefusalMessage.EmailInvalid);
    }

    /// <summary>
    /// A signed-in author is reachable by identity and needs no contact; a guest
    /// asks about an account he is locked out of here or nowhere; a ticket without
    /// a subject is named by its reason.
    /// </summary>
    [Theory]
    [InlineData(true, TicketSubtype.Bug, "guest@example.com", null)]
    [InlineData(false, TicketSubtype.Bug, null, null)]
    [InlineData(true, TicketSubtype.AccountRestoration, "guest@example.com", null)]
    [InlineData(true, TicketSubtype.PersonalData, "guest@example.com", null)]
    [InlineData(true, TicketSubtype.AgreementQuestion, "guest@example.com", null)]
    [InlineData(true, TicketSubtype.SeniorModeratorDecisionComplaint, "guest@example.com", null)]
    [InlineData(true, TicketSubtype.Bug, "guest@example.com", "")]
    public void PassAValidIntake(bool guest, TicketSubtype subtype, string? contact, string? subject)
    {
        identityProvider.Current.Returns(guest ? Identity.Guest() : Identities.User(Guid.NewGuid(), "User"));
        var input = ValidIntake();
        input.Subtype = subtype;
        input.Contact = contact;
        input.Subject = subject;

        validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
    }

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
        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.Subtype)
            .WithErrorMessage(CreateTicketIntakeValidator.SignInForThisReason);

        SetAuthenticated();
        validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
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

        var result = validator.TestValidate(input);

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

        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.ViolationUrl)
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

        validator.TestValidate(input).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RefuseTextTheRendererWillNotRender()
    {
        // Refused here, the author can still fix it.
        SetAuthenticated();
        nestingLimit.IsWithinLimit(Arg.Any<string>()).Returns(false);
        var input = ValidIntake();

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void RefuseHidingMarkup()
    {
        // A ticket renders with the tag set of profile texts, where a hiding block is shown to nobody.
        SetAuthenticated();
        var input = ValidIntake();
        input.Text = "до [private=\"Чак\"]СЕКРЕТ[/private] после";

        var result = validator.TestValidate(input);
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

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        SetAuthenticated();
        var input = ValidIntake();
        input.Text = "";

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }
}
