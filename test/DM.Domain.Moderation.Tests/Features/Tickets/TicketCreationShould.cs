using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class TicketCreationShould : TicketServiceTestBase
{
    [Fact]
    public async Task ThrowWhenUserTriesToReportThemselves()
    {
        var targetUser = new GeneralUser { UserId = currentUserId, Username = "CurrentUser" };
        userLookupService.GetAsync("CurrentUser").Returns(targetUser);

        var createTicket = new CreateTicket { TargetUsername = "CurrentUser", Description = "Bad behavior" };
        var act = () => service.CreateTicket(createTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("пожаловаться на себя"));
    }

    [Fact]
    public async Task CreateTicketWithCorrectData()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "TargetUser" };
        userLookupService.GetAsync("TargetUser").Returns(targetUser);

        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        var entityId = Guid.NewGuid();
        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            EntityId = entityId,
            EntityType = "Post",
            Description = "Spam",
            Comment = "Please review"
        };

        await service.CreateTicket(createTicket);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TicketId.Should().Be(ticketId);
        capturedEntity.ReporterUserId.Should().Be(currentUserId);
        capturedEntity.TargetUserId.Should().Be(targetUserId);
        capturedEntity.EntityId.Should().Be(entityId);
        capturedEntity.Status.Should().Be(TicketStatus.WaitingForModeration);
        capturedEntity.Subtype.Should().Be(TicketSubtype.UserComplaint);
        capturedEntity.Description.Should().Be("Spam\n\nНарушитель: TargetUser",
            "the violator is a line of the text, as the site spells his name");
    }

    [Fact]
    public async Task AnnounceACreatedTicket()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "TargetUser" };
        userLookupService.GetAsync("TargetUser").Returns(targetUser);
        ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket { TicketId = ticketId });

        await service.CreateTicket(new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "Spam"
        });

        await eventProducer.Received(1).SendAsync(EventType.TicketCreated, ticketId);
    }

    [Fact]
    public async Task ValidateCreateTicketBeforeCreating()
    {
        createValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicket>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ValidationException("invalid"));

        var act = () => service.CreateTicket(new CreateTicket { TargetUsername = "TargetUser" });

        await act.Should().ThrowAsync<ValidationException>();
        await ticketRepository.DidNotReceive().Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StripModBlockFromCreateTicketForNonModeratorReporter()
    {
        SetCurrentUser(UserRole.RegularUser);
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "TargetUser" };
        userLookupService.GetAsync("TargetUser").Returns(targetUser);

        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "[mod]secret[/mod] visible",
            Comment = "look [mod]here[/mod]"
        };

        await service.CreateTicket(createTicket);

        capturedEntity.Should().NotBeNull();
        // [mod] markers are unwrapped (inner text kept), never stored for a
        // non-moderator reporter — same rule as the public intake path.
        capturedEntity!.Description.Should().Be("secret visible\n\nНарушитель: TargetUser").And.NotContain("[mod]");
        capturedEntity.Comment.Should().Be("look here").And.NotContain("[mod]");
    }

    [Fact]
    public async Task KeepModBlockFromCreateTicketForModeratorReporter()
    {
        SetCurrentUser(UserRole.Moderator);
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "TargetUser" };
        userLookupService.GetAsync("TargetUser").Returns(targetUser);

        CreateTicketEntity? capturedEntity = null;
        ticketRepository.Create(Arg.Do<CreateTicketEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ticket());

        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "[mod]note[/mod]",
            Comment = "plain"
        };

        await service.CreateTicket(createTicket);

        capturedEntity!.Description.Should().Be("[mod]note[/mod]\n\nНарушитель: TargetUser");
    }
}
