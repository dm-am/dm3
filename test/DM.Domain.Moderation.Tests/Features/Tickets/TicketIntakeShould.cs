using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

/// <summary>
/// A ticket filed from the support and complaint forms, by a guest or from an
/// account: the fields of the form as lines of its text, the violator it
/// names, the address of a guest and his tracking token.
/// </summary>
public class TicketIntakeShould : TicketServiceTestBase
{
    [Fact]
    public async Task CreateIntakeTicketForAuthenticatedUser()
    {
        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        var createTicketIntake = new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = "Bug subject",
            Text = "Bug text",
            Contact = "user@example.com"
        };

        await service.CreateIntakeTicket(createTicketIntake);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.ReporterUserId.Should().Be(currentUserId);
        capturedEntity.TargetUserId.Should().BeNull();
        // Authenticated authors are reachable by identity, the contact is
        // preserved inside the ticket body instead of GuestEmail
        capturedEntity.GuestEmail.Should().BeNull();
        capturedEntity.Description.Should().Contain("Bug text")
            .And.Contain("user@example.com");
        capturedEntity.Comment.Should().Be("Bug subject");
        capturedEntity.Status.Should().Be(TicketStatus.WaitingForModeration);
        capturedEntity.Subtype.Should().Be(TicketSubtype.Bug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateIntakeTicketWithoutSubject(string? subject)
    {
        // The forms send no subject, and the text arrives as written with the contact a line under it.
        identityProvider.Current.Returns(Identity.Guest());

        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        await service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = subject,
            Text = "[b]Страница[/b] не открывается",
            Contact = "guest@example.com"
        });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Comment.Should().BeEmpty();
        capturedEntity.Description.Should().Be(
            "[b]Страница[/b] не открывается\n\nКонтакт для ответа: guest@example.com");
    }

    /// <summary>
    /// The violator of an intake complaint is a name the reader typed, and what
    /// is wrong with it stands under that field.
    /// </summary>
    [Fact]
    public async Task RefuseAnUnknownViolatorUnderItsField()
    {
        userLookupService.FindUserIdAsync("Nobody", Arg.Any<CancellationToken>()).Returns((false, Guid.Empty));

        var act = () => service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.UserComplaint,
            Text = "Оскорбляет в комментариях",
            TargetUsername = "Nobody",
            ViolationUrl = "https://dm.am/forum/flood/1"
        });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.ValidationErrors.Should().ContainKey("targetUsername")
            .WhoseValue.Should().Be(RefusalMessage.UserNotFound);
        await userLookupService.DidNotReceive().GetAsync(Arg.Any<string>());
        await ticketRepository.DidNotReceive().Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseAnIntakeComplaintAboutOneselfUnderTheViolatorField()
    {
        userLookupService.FindUserIdAsync("CurrentUser", Arg.Any<CancellationToken>()).Returns((true, currentUserId));

        var act = () => service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.UserComplaint,
            Text = "Оскорбляю сам себя",
            TargetUsername = "CurrentUser",
            ViolationUrl = "https://dm.am/forum/flood/1"
        });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.ValidationErrors.Should().ContainKey("targetUsername");
        await ticketRepository.DidNotReceive().Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The violator a complaint names is filed on the ticket, for the rule that
    /// keeps the complaint away from him, and written into its text as a line, as
    /// the site spells his name: the ticket is read as its text and nothing beside
    /// it.
    /// </summary>
    [Fact]
    public async Task FileTheViolatorOfAnIntakeComplaintOnTheTicket()
    {
        userLookupService.FindUserIdAsync("targetuser", Arg.Any<CancellationToken>()).Returns((true, targetUserId));
        userLookupService.GetAsync(targetUserId)
            .Returns(new GeneralUser { UserId = targetUserId, Username = "TargetUser" });
        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        await service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.UserComplaint,
            Text = "Оскорбляет в [b]комментариях[/b]",
            TargetUsername = " targetuser ",
            ViolationUrl = "https://dm.am/forum/flood/1",
            Contact = "в личных сообщениях"
        });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TargetUserId.Should().Be(targetUserId,
            "a complaint about a moderator stays away from him (TicketVisibility)");
        capturedEntity.Description.Should().Be(
            "Оскорбляет в [b]комментариях[/b]\n\n" +
            "Нарушитель: TargetUser\n\n" +
            "Ссылка на нарушение: https://dm.am/forum/flood/1\n\n" +
            "Контакт для ответа: в личных сообщениях",
            "the fields of the form follow the text in the order the form asks them");
    }

    /// <summary>
    /// Account restoration is open to a guest: the person who wants the account
    /// back cannot sign in.
    /// </summary>
    [Theory]
    [InlineData(TicketSubtype.AccessRecovery)]
    [InlineData(TicketSubtype.AccountRestoration)]
    public async Task CreateIntakeTicketForGuestWithGuestEmail(TicketSubtype subtype)
    {
        identityProvider.Current.Returns(Identity.Guest());

        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        var createTicketIntake = new CreateTicketIntake
        {
            Subtype = subtype,
            Subject = "Lost access",
            Text = "Cannot login",
            Contact = "guest@example.com"
        };

        await service.CreateIntakeTicket(createTicketIntake);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.ReporterUserId.Should().BeNull();
        capturedEntity.GuestEmail.Should().Be("guest@example.com");
        capturedEntity.Description.Should().Be(
            "Cannot login\n\nКонтакт для ответа: guest@example.com",
            "moderation reads a guest's address in the text, as an author's contact is read");
        capturedEntity.Subtype.Should().Be(subtype);
        capturedEntity.Status.Should().Be(TicketStatus.WaitingForModeration);
    }

    [Fact]
    public async Task GenerateTrackingTokenForGuestIntakeOnly()
    {
        // Guests get an unguessable one-time tracking token; authenticated
        // authors get none (they use "Мои обращения").
        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        identityProvider.Current.Returns(Identity.Guest());
        await service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = "Guest subject",
            Text = "Guest text",
            Contact = "guest@example.com"
        });

        capturedEntity.Should().NotBeNull();
        // 128 bits as lower-case hex: the token is the guest's only key to the ticket.
        capturedEntity!.TrackingToken.Should().MatchRegex("^[0-9a-f]{32}$");

        capturedEntity = null;
        SetCurrentUser(UserRole.RegularUser);
        await service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = "User subject",
            Text = "User text"
        });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TrackingToken.Should().BeNull();
    }

    /// <summary>
    /// The link a complaint carries is called what the field of the form called
    /// it: the decision for a complaint about a decision, the violation for
    /// every other one.
    /// </summary>
    [Theory]
    [InlineData(TicketSubtype.ModeratorDecisionComplaint, "Ссылка на решение")]
    [InlineData(TicketSubtype.SeniorModeratorDecisionComplaint, "Ссылка на решение")]
    [InlineData(TicketSubtype.UserComplaint, "Ссылка на нарушение")]
    public async Task CallTheLinkOfAComplaintWhatTheFormCalledIt(TicketSubtype subtype, string caption)
    {
        SetCurrentUser(UserRole.RegularUser);
        userLookupService.FindUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, targetUserId));
        userLookupService.GetAsync(targetUserId)
            .Returns(new GeneralUser { UserId = targetUserId, Username = "TargetUser" });
        CreateTicketEntity? captured = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        await service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = subtype,
            Text = "Не согласен",
            TargetUsername = subtype == TicketSubtype.UserComplaint ? "TargetUser" : null,
            ViolationUrl = "https://dm.am/warnings?username=CurrentUser"
        });

        captured.Should().NotBeNull();
        captured!.Description.Should().EndWith($"{caption}: https://dm.am/warnings?username=CurrentUser");
    }
}
