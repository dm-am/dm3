using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class CreateTicketIntakeValidatorShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly CreateTicketIntakeValidator _validator;

    public CreateTicketIntakeValidatorShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _validator = new CreateTicketIntakeValidator(_identityProvider.Object);
    }

    private void SetGuest() =>
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

    private void SetAuthenticated() =>
        _identityProvider.Setup(p => p.Current).Returns(Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token"));

    private static CreateTicketIntake ValidIntake() => new()
    {
        Subtype = TicketSubtype.Bug,
        Subject = "Subject",
        Text = "Body text"
    };

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

    [Fact]
    public void FailWhenSubjectIsEmpty()
    {
        SetAuthenticated();
        var input = ValidIntake();
        input.Subject = "";

        var result = _validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage(ValidationError.Empty);
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
