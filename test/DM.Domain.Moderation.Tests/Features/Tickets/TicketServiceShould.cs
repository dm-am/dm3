using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class TicketServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateTicket>> _createValidator;
    private readonly Mock<IValidator<CreateTicketIntake>> _createIntakeValidator;
    private readonly Mock<IValidator<ResolveTicket>> _resolveValidator;
    private readonly Mock<ITicketRepository> _ticketRepository;
    private readonly Mock<IWarningService> _warningService;
    private readonly Mock<IBanService> _banService;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly TicketService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public TicketServiceShould()
    {
        _createValidator = Mock<IValidator<CreateTicket>>();
        _createValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateTicket>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _createIntakeValidator = Mock<IValidator<CreateTicketIntake>>();
        _createIntakeValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateTicketIntake>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _resolveValidator = Mock<IValidator<ResolveTicket>>();
        _resolveValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ResolveTicket>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _ticketRepository = Mock<ITicketRepository>();
        _warningService = Mock<IWarningService>();
        _banService = Mock<IBanService>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        SetCurrentUser(UserRole.Moderator);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_ticketId);

        _service = new TicketService(
            _createValidator.Object,
            _createIntakeValidator.Object,
            _resolveValidator.Object,
            _ticketRepository.Object,
            _warningService.Object,
            _banService.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _eventProducer.Object);
    }

    private void SetCurrentUser(UserRole role)
    {
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = role, Username = "CurrentUser" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(identity);
    }

    [Fact]
    public async Task ThrowWhenUserTriesToReportThemselves()
    {
        var targetUser = new GeneralUser { UserId = _currentUserId, Username = "CurrentUser" };
        _userLookupService.Setup(s => s.GetAsync("CurrentUser")).ReturnsAsync(targetUser);

        var createTicket = new CreateTicket { TargetUsername = "CurrentUser", Description = "Bad behavior" };
        var act = () => _service.CreateTicket(createTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("пожаловаться на себя"));
    }

    [Fact]
    public async Task CreateTicketWithCorrectData()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.Setup(s => s.GetAsync("TargetUser")).ReturnsAsync(targetUser);

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ticket());

        var entityId = Guid.NewGuid();
        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            EntityId = entityId,
            EntityType = "Post",
            Description = "Spam",
            Comment = "Please review"
        };

        await _service.CreateTicket(createTicket);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TicketId.Should().Be(_ticketId);
        capturedEntity.ReporterUserId.Should().Be(_currentUserId);
        capturedEntity.TargetUserId.Should().Be(_targetUserId);
        capturedEntity.EntityId.Should().Be(entityId);
        capturedEntity.Status.Should().Be(TicketStatus.WaitingForModeration);
        capturedEntity.Subtype.Should().Be(TicketSubtype.UserComplaint);
    }

    [Fact]
    public async Task AnnounceACreatedTicket()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.Setup(s => s.GetAsync("TargetUser")).ReturnsAsync(targetUser);
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ticket { TicketId = _ticketId });

        await _service.CreateTicket(new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "Spam"
        });

        _eventProducer.Verify(p => p.SendAsync(EventType.TicketCreated, _ticketId), Times.Once);
    }

    [Fact]
    public async Task ValidateCreateTicketBeforeCreating()
    {
        _createValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateTicket>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("invalid"));

        var act = () => _service.CreateTicket(new CreateTicket { TargetUsername = "TargetUser" });

        await act.Should().ThrowAsync<ValidationException>();
        _ticketRepository.Verify(
            r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StripModBlockFromCreateTicketForNonModeratorReporter()
    {
        SetCurrentUser(UserRole.RegularUser);
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.Setup(s => s.GetAsync("TargetUser")).ReturnsAsync(targetUser);

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ticket());

        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "[mod]secret[/mod] visible",
            Comment = "look [mod]here[/mod]"
        };

        await _service.CreateTicket(createTicket);

        capturedEntity.Should().NotBeNull();
        // [mod] markers are unwrapped (inner text kept), never stored for a
        // non-moderator reporter — same rule as the public intake path.
        capturedEntity!.Description.Should().Be("secret visible").And.NotContain("[mod]");
        capturedEntity.Comment.Should().Be("look here").And.NotContain("[mod]");
    }

    [Fact]
    public async Task KeepModBlockFromCreateTicketForModeratorReporter()
    {
        SetCurrentUser(UserRole.Moderator);
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.Setup(s => s.GetAsync("TargetUser")).ReturnsAsync(targetUser);

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ticket());

        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "[mod]note[/mod]",
            Comment = "plain"
        };

        await _service.CreateTicket(createTicket);

        capturedEntity!.Description.Should().Be("[mod]note[/mod]");
    }

    [Fact]
    public async Task CreateIntakeTicketForAuthenticatedUser()
    {
        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ticket());

        var createTicketIntake = new CreateTicketIntake
        {
            Subtype = TicketSubtype.SiteImprovementSuggestion,
            Subject = "Suggestion subject",
            Text = "Suggestion text",
            Contact = "user@example.com"
        };

        await _service.CreateIntakeTicket(createTicketIntake);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.ReporterUserId.Should().Be(_currentUserId);
        capturedEntity.TargetUserId.Should().BeNull();
        // Authenticated authors are reachable by identity, the contact is
        // preserved inside the ticket body instead of GuestEmail
        capturedEntity.GuestEmail.Should().BeNull();
        capturedEntity.Description.Should().Contain("Suggestion text")
            .And.Contain("user@example.com");
        capturedEntity.Comment.Should().Be("Suggestion subject");
        capturedEntity.Status.Should().Be(TicketStatus.WaitingForModeration);
        capturedEntity.Subtype.Should().Be(TicketSubtype.SiteImprovementSuggestion);
    }

    [Fact]
    public async Task CreateIntakeTicketForGuestWithGuestEmail()
    {
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ticket());

        var createTicketIntake = new CreateTicketIntake
        {
            Subtype = TicketSubtype.AccessRecovery,
            Subject = "Lost access",
            Text = "Cannot login",
            Contact = "guest@example.com"
        };

        await _service.CreateIntakeTicket(createTicketIntake);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.ReporterUserId.Should().BeNull();
        capturedEntity.GuestEmail.Should().Be("guest@example.com");
        capturedEntity.Subtype.Should().Be(TicketSubtype.AccessRecovery);
    }

    [Fact]
    public async Task FilterTicketSubtypesByCallerRole()
    {
        IReadOnlyCollection<TicketSubtype>? capturedSubtypes = null;
        _ticketRepository.Setup(r => r.GetTickets(
                It.IsAny<PagingQuery>(),
                It.IsAny<TicketStatus?>(),
                It.IsAny<IReadOnlyCollection<TicketSubtype>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<PagingQuery, TicketStatus?, IReadOnlyCollection<TicketSubtype>?, CancellationToken>(
                (_, _, subtypes, _) => capturedSubtypes = subtypes)
            .ReturnsAsync((Array.Empty<Ticket>(), PagingResult.Empty(20)));

        // Junior moderator: user complaints and suggestions only
        SetCurrentUser(UserRole.Moderator);
        await _service.GetTickets(new PagingQuery());
        capturedSubtypes.Should().BeEquivalentTo(
            new[] { TicketSubtype.UserComplaint, TicketSubtype.SiteImprovementSuggestion });

        // Senior moderator: additionally complaints about junior moderator decisions
        SetCurrentUser(UserRole.SeniorModerator);
        await _service.GetTickets(new PagingQuery());
        capturedSubtypes.Should().BeEquivalentTo(new[]
        {
            TicketSubtype.UserComplaint,
            TicketSubtype.SiteImprovementSuggestion,
            TicketSubtype.ModeratorDecisionComplaint
        });

        // Admin: no subtype filter at all
        SetCurrentUser(UserRole.Admin);
        await _service.GetTickets(new PagingQuery());
        capturedSubtypes.Should().BeNull();
    }

    [Fact]
    public async Task ReturnEmptyWhenRequestedSubtypeIsOutOfScope()
    {
        SetCurrentUser(UserRole.Moderator);

        var (tickets, _) = await _service.GetTickets(new PagingQuery(), subtype: TicketSubtype.Bug);

        tickets.Should().BeEmpty();
        _ticketRepository.Verify(r => r.GetTickets(
                It.IsAny<PagingQuery>(),
                It.IsAny<TicketStatus?>(),
                It.IsAny<IReadOnlyCollection<TicketSubtype>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReturnTicketToItsReporterRegardlessOfSubtypeScope()
    {
        // A regular user (below Moderator) can always read their own ticket,
        // even one in an admin-only subtype.
        SetCurrentUser(UserRole.RegularUser);
        var ticket = new TicketDetails
        {
            TicketId = _ticketId,
            ReporterUserId = _currentUserId,
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Setup(r => r.GetDetails(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var result = await _service.GetTicket(_ticketId);

        result.Should().BeSameAs(ticket);
    }

    [Fact]
    public async Task DenyTicketToNonReporterModeratorOnAdminOnlySubtype()
    {
        // Plain moderator, not the reporter, requesting an admin-only subtype
        // by GUID: 404 (not 403) so the endpoint is not an existence oracle.
        SetCurrentUser(UserRole.Moderator);
        var ticket = new TicketDetails
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Setup(r => r.GetDetails(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _service.GetTicket(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTicketToInScopeModerator()
    {
        SetCurrentUser(UserRole.Moderator);
        var ticket = new TicketDetails
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.UserComplaint
        };
        _ticketRepository.Setup(r => r.GetDetails(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var result = await _service.GetTicket(_ticketId);

        result.Should().BeSameAs(ticket);
    }

    [Fact]
    public async Task ReturnTicketOfAnySubtypeToAdmin()
    {
        SetCurrentUser(UserRole.Admin);
        var ticket = new TicketDetails
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Setup(r => r.GetDetails(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var result = await _service.GetTicket(_ticketId);

        result.Should().BeSameAs(ticket);
    }

    [Fact]
    public async Task DenyMissingTicketWithNotFound()
    {
        _ticketRepository.Setup(r => r.GetDetails(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDetails?)null);

        var act = () => _service.GetTicket(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowWhenAssigningNonexistentTicket()
    {
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var act = () => _service.AssignToMe(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдено"));
    }

    [Fact]
    public async Task ThrowWhenAssigningOutOfScopeSubtypeTicket()
    {
        // Plain moderator assigning an admin-only subtype ticket by GUID: 404.
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _service.AssignToMe(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        _ticketRepository.Verify(
            r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ThrowWhenAssigningClosedTicket()
    {
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.Closed };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _service.AssignToMe(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("нельзя взять в работу"));
    }

    [Fact]
    public async Task AssignTicketToCurrentUser()
    {
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.WaitingForModeration };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        UpdateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(ticket);

        await _service.AssignToMe(_ticketId);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AssignedModeratorId.Should().Be(_currentUserId);
        // Assignment does not change the doc status model
        capturedEntity.Status.Should().BeNull();
    }

    [Fact]
    public async Task ValidateResolveTicketBeforeResolving()
    {
        _resolveValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<ResolveTicket>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("invalid"));

        var act = () => _service.ResolveTicket(_ticketId, new ResolveTicket { Status = TicketStatus.Closed });

        await act.Should().ThrowAsync<ValidationException>();
        _ticketRepository.Verify(r => r.Get(_ticketId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ThrowWhenResolvingOutOfScopeSubtypeTicket()
    {
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _service.ResolveTicket(_ticketId, new ResolveTicket { Status = TicketStatus.Closed });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowWhenResolvingAlreadyClosedTicket()
    {
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.Closed };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket { Status = TicketStatus.Closed };
        var act = () => _service.ResolveTicket(_ticketId, resolveTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("уже закрыто"));
    }

    [Fact]
    public async Task ThrowWhenIssuingWarningForTicketWithoutTarget()
    {
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForModeration,
            TargetUsername = null
        };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            IssueWarning = true,
            WarningText = "Warning text",
            WarningPoints = 1
        };
        var act = () => _service.ResolveTicket(_ticketId, resolveTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не указан пользователь"));
    }

    [Fact]
    public async Task IssueWarningThroughWarningServiceWhenResolvingTicketWithWarning()
    {
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForUser,
            TargetUsername = "TargetUser"
        };

        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _warningService.Setup(s => s.CreateWarning(It.IsAny<CreateWarning>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Warning { WarningId = Guid.NewGuid() });
        _ticketRepository.Setup(r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            IssueWarning = true,
            WarningText = "Warning text",
            WarningPoints = 2
        };

        await _service.ResolveTicket(_ticketId, resolveTicket);

        // Routed through the warning service (which owns the clamp + gate),
        // never straight to the repository.
        _warningService.Verify(s => s.CreateWarning(
            It.Is<CreateWarning>(w => w.Username == "TargetUser" && w.Reason == "Warning text" && w.Points == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectBanIssuanceByPlainModerator()
    {
        // A plain moderator resolving a ticket cannot issue a ban: 403, and the
        // ban service is never reached.
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint,
            TargetUsername = "TargetUser"
        };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            IssueBan = true,
            BanDurationHours = 24,
            BanComment = "Banned for spam"
        };
        var act = () => _service.ResolveTicket(_ticketId, resolveTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        _banService.Verify(
            s => s.CreateBan(It.IsAny<CreateBan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueBanThroughBanServiceForSeniorModerator()
    {
        SetCurrentUser(UserRole.SeniorModerator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint,
            TargetUsername = "TargetUser"
        };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _banService.Setup(s => s.CreateBan(It.IsAny<CreateBan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = Guid.NewGuid() });
        _ticketRepository.Setup(r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            IssueBan = true,
            BanDurationHours = 24,
            BanComment = "Banned for spam"
        };

        await _service.ResolveTicket(_ticketId, resolveTicket);

        // Routed through the ban service (which owns the senior-mod gate and the
        // already-banned conflict check), never straight to the repository.
        _banService.Verify(s => s.CreateBan(
            It.Is<CreateBan>(b => b.Username == "TargetUser" && b.DurationHours == 24 && !b.IsVoluntary),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PropagateBanServiceConflictOnAlreadyBannedTarget()
    {
        SetCurrentUser(UserRole.SeniorModerator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint,
            TargetUsername = "TargetUser"
        };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _banService.Setup(s => s.CreateBan(It.IsAny<CreateBan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpException(HttpStatusCode.Conflict,
                "Пользователь TargetUser уже забанен до ..."));

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Closed,
            IssueBan = true,
            BanDurationHours = 24,
            BanComment = "Banned for spam"
        };
        var act = () => _service.ResolveTicket(_ticketId, resolveTicket);

        // The conflict check inside the ban service fires and is not swallowed.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict)
            .Where(e => e.Message.Contains("уже забанен"));
        _ticketRepository.Verify(
            r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PassStatusAndSubtypeFiltersToRepositoryForMyFiledTickets()
    {
        // "Мои обращения" filters are server-side: the service forwards the
        // status and subtype straight to the repository (the reporter owns the
        // tickets, so no subtype visibility gating is applied).
        Guid capturedUserId = Guid.Empty;
        TicketStatus? capturedStatus = null;
        TicketSubtype? capturedSubtype = null;
        _ticketRepository.Setup(r => r.GetUserTickets(
                It.IsAny<Guid>(), It.IsAny<TicketStatus?>(), It.IsAny<TicketSubtype?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, TicketStatus?, TicketSubtype?, CancellationToken>(
                (userId, status, subtype, _) =>
                {
                    capturedUserId = userId;
                    capturedStatus = status;
                    capturedSubtype = subtype;
                })
            .ReturnsAsync(Array.Empty<Ticket>());

        await _service.GetMyFiledTickets(TicketStatus.Closed, TicketSubtype.Bug);

        capturedUserId.Should().Be(_currentUserId);
        capturedStatus.Should().Be(TicketStatus.Closed);
        capturedSubtype.Should().Be(TicketSubtype.Bug);
    }

    [Fact]
    public async Task ReturnTicketByTrackingToken()
    {
        // Guest tracking is token-gated: possession of the token returns the
        // ticket regardless of the caller identity.
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        var ticket = new TicketDetails { TicketId = _ticketId, Status = TicketStatus.WaitingForUser };
        _ticketRepository.Setup(r => r.GetByTrackingToken("tok123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var result = await _service.GetTicketByTrackingToken("tok123");

        result.Should().BeSameAs(ticket);
    }

    [Fact]
    public async Task ReturnNullForUnknownTrackingToken()
    {
        _ticketRepository.Setup(r => r.GetByTrackingToken(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDetails?)null);

        var result = await _service.GetTicketByTrackingToken("nope");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateTrackingTokenForGuestIntakeOnly()
    {
        // Guests get an unguessable one-time tracking token; authenticated
        // authors get none (they use "Мои обращения").
        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Create(It.IsAny<CreateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new Ticket());

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        await _service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = "Guest subject",
            Text = "Guest text",
            Contact = "guest@example.com"
        });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TrackingToken.Should().NotBeNullOrWhiteSpace();

        capturedEntity = null;
        SetCurrentUser(UserRole.RegularUser);
        await _service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = "User subject",
            Text = "User text"
        });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TrackingToken.Should().BeNull();
    }
}
