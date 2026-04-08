using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class TicketServiceShould : UnitTestBase
{
    private readonly Mock<ITicketRepository> _ticketRepository;
    private readonly Mock<IWarningRepository> _warningRepository;
    private readonly Mock<IBanRepository> _banRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly TicketService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public TicketServiceShould()
    {
        _ticketRepository = Mock<ITicketRepository>();
        _warningRepository = Mock<IWarningRepository>();
        _banRepository = Mock<IBanRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = UserRole.Moderator, Username = "CurrentUser" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_ticketId);

        _service = new TicketService(
            _ticketRepository.Object,
            _warningRepository.Object,
            _banRepository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task ThrowWhenUserTriesToReportThemselves()
    {
        var targetUser = new GeneralUser { UserId = _currentUserId, Username = "CurrentUser" };
        _userLookupService.Setup(s => s.GetAsync("CurrentUser")).ReturnsAsync(targetUser);

        var createTicket = new CreateTicket { TargetUsername = "CurrentUser", Description = "Bad behavior" };
        var act = () => _service.CreateTicket(createTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Cannot report yourself"));
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
        capturedEntity.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task ThrowWhenAssigningNonexistentTicket()
    {
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var act = () => _service.AssignToMe(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("not found"));
    }

    [Fact]
    public async Task ThrowWhenAssigningNonOpenTicket()
    {
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.Resolved };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var act = () => _service.AssignToMe(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("only assign open"));
    }

    [Fact]
    public async Task AssignTicketToCurrentUser()
    {
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.Open };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        UpdateTicketEntity? capturedEntity = null;
        _ticketRepository.Setup(r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTicketEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(ticket);

        await _service.AssignToMe(_ticketId);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.AssignedModeratorId.Should().Be(_currentUserId);
        capturedEntity.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public async Task ThrowWhenResolvingAlreadyClosedTicket()
    {
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.Resolved };
        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket { Status = TicketStatus.Resolved };
        var act = () => _service.ResolveTicket(_ticketId, resolveTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("already closed"));
    }

    [Fact]
    public async Task IssueWarningWhenResolvingTicketWithWarning()
    {
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.InProgress,
            TargetUsername = "TargetUser"
        };
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };

        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _userLookupService.Setup(s => s.GetAsync(_targetUserId)).ReturnsAsync(targetUser);
        _userLookupService.Setup(s => s.GetAsync("TargetUser")).ReturnsAsync(targetUser);
        _warningRepository.Setup(r => r.Create(It.IsAny<CreateWarningEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Warning { WarningId = Guid.NewGuid() });
        _ticketRepository.Setup(r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Resolved,
            IssueWarning = true,
            WarningText = "Warning text",
            WarningPoints = 2
        };

        await _service.ResolveTicket(_ticketId, resolveTicket);

        _warningRepository.Verify(r => r.Create(
            It.Is<CreateWarningEntity>(e => e.Text == "Warning text" && e.Points == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueBanWhenResolvingTicketWithBan()
    {
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.InProgress,
            TargetUsername = "TargetUser"
        };
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };

        _ticketRepository.Setup(r => r.Get(_ticketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);
        _userLookupService.Setup(s => s.GetAsync(_targetUserId)).ReturnsAsync(targetUser);
        _userLookupService.Setup(s => s.GetAsync("TargetUser")).ReturnsAsync(targetUser);
        _banRepository.Setup(r => r.Create(It.IsAny<CreateBanEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ban { BanId = Guid.NewGuid() });
        _ticketRepository.Setup(r => r.Update(It.IsAny<UpdateTicketEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveTicket = new ResolveTicket
        {
            Status = TicketStatus.Resolved,
            IssueBan = true,
            BanDurationHours = 24,
            BanComment = "Banned for spam"
        };

        await _service.ResolveTicket(_ticketId, resolveTicket);

        _banRepository.Verify(r => r.Create(
            It.Is<CreateBanEntity>(e => e.Comment == "Banned for spam"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
