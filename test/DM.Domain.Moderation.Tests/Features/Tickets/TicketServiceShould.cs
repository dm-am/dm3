using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using DM.Testing.Markup;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class TicketServiceShould : UnitTestBase
{
    private readonly IValidator<CreateTicket> _createValidator;
    private readonly IValidator<CreateTicketIntake> _createIntakeValidator;
    private readonly IValidator<CreateTicketComment> _createCommentValidator;
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventProducer _eventProducer;
    private readonly TicketService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _commentId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public TicketServiceShould()
    {
        _createValidator = Mock<IValidator<CreateTicket>>();
        _createValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicket>>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _createIntakeValidator = Mock<IValidator<CreateTicketIntake>>();
        _createIntakeValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicketIntake>>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _createCommentValidator = Mock<IValidator<CreateTicketComment>>();
        _createCommentValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicketComment>>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _ticketRepository = Mock<ITicketRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _eventProducer = Mock<IEventProducer>();

        SetCurrentUser(UserRole.Moderator);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_ticketId);
        _ticketRepository.GetComments(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TicketComment>());

        _service = new TicketService(
            _createValidator,
            _createIntakeValidator,
            _createCommentValidator,
            _ticketRepository,
            _userLookupService,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            _eventProducer,
            new TagNamesAsTyped());
    }

    private void SetCurrentUser(UserRole role)
    {
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = role, Username = "CurrentUser" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(identity);
    }

    [Fact]
    public async Task ThrowWhenUserTriesToReportThemselves()
    {
        var targetUser = new GeneralUser { UserId = _currentUserId, Username = "CurrentUser" };
        _userLookupService.GetAsync("CurrentUser").Returns(targetUser);

        var createTicket = new CreateTicket { TargetUsername = "CurrentUser", Description = "Bad behavior" };
        var act = () => _service.CreateTicket(createTicket);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("пожаловаться на себя"));
    }

    [Fact]
    public async Task CreateTicketWithCorrectData()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.GetAsync("TargetUser").Returns(targetUser);

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateTicketEntity>(0);
                capturedEntity = e;
            });

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
        capturedEntity.Description.Should().Be("Spam\n\nНарушитель: TargetUser",
            "the violator is a line of the text, as the site spells his name");
    }

    [Fact]
    public async Task AnnounceACreatedTicket()
    {
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.GetAsync("TargetUser").Returns(targetUser);
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket { TicketId = _ticketId });

        await _service.CreateTicket(new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "Spam"
        });

        await _eventProducer.Received(1).SendAsync(EventType.TicketCreated, _ticketId);
    }

    [Fact]
    public async Task ValidateCreateTicketBeforeCreating()
    {
        _createValidator.ValidateAsync(
                Arg.Any<ValidationContext<CreateTicket>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ValidationException("invalid"));

        var act = () => _service.CreateTicket(new CreateTicket { TargetUsername = "TargetUser" });

        await act.Should().ThrowAsync<ValidationException>();
        await _ticketRepository.DidNotReceive().Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StripModBlockFromCreateTicketForNonModeratorReporter()
    {
        SetCurrentUser(UserRole.RegularUser);
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.GetAsync("TargetUser").Returns(targetUser);

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateTicketEntity>(0);
                capturedEntity = e;
            });

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
        capturedEntity!.Description.Should().Be("secret visible\n\nНарушитель: TargetUser").And.NotContain("[mod]");
        capturedEntity.Comment.Should().Be("look here").And.NotContain("[mod]");
    }

    [Fact]
    public async Task KeepModBlockFromCreateTicketForModeratorReporter()
    {
        SetCurrentUser(UserRole.Moderator);
        var targetUser = new GeneralUser { UserId = _targetUserId, Username = "TargetUser" };
        _userLookupService.GetAsync("TargetUser").Returns(targetUser);

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateTicketEntity>(0);
                capturedEntity = e;
            });

        var createTicket = new CreateTicket
        {
            TargetUsername = "TargetUser",
            Description = "[mod]note[/mod]",
            Comment = "plain"
        };

        await _service.CreateTicket(createTicket);

        capturedEntity!.Description.Should().Be("[mod]note[/mod]\n\nНарушитель: TargetUser");
    }

    [Fact]
    public async Task CreateIntakeTicketForAuthenticatedUser()
    {
        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateTicketEntity>(0);
                capturedEntity = e;
            });

        var createTicketIntake = new CreateTicketIntake
        {
            Subtype = TicketSubtype.Bug,
            Subject = "Bug subject",
            Text = "Bug text",
            Contact = "user@example.com"
        };

        await _service.CreateIntakeTicket(createTicketIntake);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.ReporterUserId.Should().Be(_currentUserId);
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
        // The forms send no subject. The ticket stores an empty one, which is
        // what the ticket screens replace with the category name, and the text
        // arrives as the author wrote it, markup included, the contact a line
        // under it.
        _identityProvider.Current.Returns(Identity.Guest());

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci => capturedEntity = ci.ArgAt<CreateTicketEntity>(0));

        await _service.CreateIntakeTicket(new CreateTicketIntake
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
    /// <remarks>
    /// The lookup used to throw its own 404, which the form could only put over
    /// the button: the reader did not learn that it was the name, and not the
    /// whole complaint, that the site had not found.
    /// </remarks>
    [Fact]
    public async Task RefuseAnUnknownViolatorUnderItsField()
    {
        _userLookupService.FindUserIdAsync("Nobody", Arg.Any<CancellationToken>()).Returns((false, Guid.Empty));

        var act = () => _service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.UserComplaint,
            Text = "Оскорбляет в комментариях",
            TargetUsername = "Nobody",
            ViolationUrl = "https://dm.am/forum/flood/1"
        });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.ValidationErrors.Should().ContainKey("targetUsername")
            .WhoseValue.Should().Be(RefusalMessage.UserNotFound);
        await _userLookupService.DidNotReceive().GetAsync(Arg.Any<string>());
        await _ticketRepository.DidNotReceive().Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseAnIntakeComplaintAboutOneselfUnderTheViolatorField()
    {
        _userLookupService.FindUserIdAsync("CurrentUser", Arg.Any<CancellationToken>()).Returns((true, _currentUserId));

        var act = () => _service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.UserComplaint,
            Text = "Оскорбляю сам себя",
            TargetUsername = "CurrentUser",
            ViolationUrl = "https://dm.am/forum/flood/1"
        });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.ValidationErrors.Should().ContainKey("targetUsername");
        await _ticketRepository.DidNotReceive().Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>());
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
        _userLookupService.FindUserIdAsync("targetuser", Arg.Any<CancellationToken>()).Returns((true, _targetUserId));
        _userLookupService.GetAsync(_targetUserId)
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "TargetUser" });
        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci => capturedEntity = ci.ArgAt<CreateTicketEntity>(0));

        await _service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = TicketSubtype.UserComplaint,
            Text = "Оскорбляет в [b]комментариях[/b]",
            TargetUsername = " targetuser ",
            ViolationUrl = "https://dm.am/forum/flood/1",
            Contact = "в личных сообщениях"
        });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TargetUserId.Should().Be(_targetUserId,
            "a complaint about a moderator stays away from him (TicketVisibility)");
        capturedEntity.Description.Should().Be(
            "Оскорбляет в [b]комментариях[/b]\n\n" +
            "Нарушитель: TargetUser\n\n" +
            "Ссылка на нарушение: https://dm.am/forum/flood/1\n\n" +
            "Контакт для ответа: в личных сообщениях",
            "the fields of the form follow the text in the order the form asks them");
    }

    [Fact]
    public async Task CreateIntakeTicketForGuestWithGuestEmail()
    {
        _identityProvider.Current.Returns(Identity.Guest());

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateTicketEntity>(0);
                capturedEntity = e;
            });

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
        capturedEntity.Description.Should().Be(
            "Cannot login\n\nКонтакт для ответа: guest@example.com",
            "moderation reads a guest's address in the text, as an author's contact is read");
        capturedEntity.Subtype.Should().Be(TicketSubtype.AccessRecovery);
    }

    [Theory]
    [InlineData(TicketSubtype.AccountRestoration)]
    public async Task CreateIntakeTicketForGuestAskingAboutAccountRestoration(TicketSubtype subtype)
    {
        // Asking for an account to be brought back is an ordinary support ticket
        // and must be open to a guest: the person who wants the account back is
        // by definition unable to sign in. The form performs no action on the
        // account - it files the request and nothing else. Removal is filed from
        // the account itself (CreateTicketIntakeValidator).
        _identityProvider.Current.Returns(Identity.Guest());

        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci => capturedEntity = ci.ArgAt<CreateTicketEntity>(0));

        var createTicketIntake = new CreateTicketIntake
        {
            Subtype = subtype,
            Subject = "Account request",
            Text = "Please handle my account",
            Contact = "guest@example.com"
        };

        await _service.CreateIntakeTicket(createTicketIntake);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Subtype.Should().Be(subtype);
        capturedEntity.ReporterUserId.Should().BeNull();
        capturedEntity.GuestEmail.Should().Be("guest@example.com");
        capturedEntity.TrackingToken.Should().NotBeNullOrEmpty();
        capturedEntity.Status.Should().Be(TicketStatus.WaitingForModeration);
    }

    [Theory]
    [InlineData(TicketSubtype.AccountRemoval)]
    [InlineData(TicketSubtype.AccountRestoration)]
    [InlineData(TicketSubtype.PersonalData)]
    [InlineData(TicketSubtype.AgreementQuestion)]
    public async Task ScopeAccountSubtypesToAdministrators(TicketSubtype subtype)
    {
        // Support-desk scope, like AccessRecovery: these are claims about who
        // owns an account, and the queue that reads them is the administrators'
        // one. A moderator asking for the category by hand gets an empty page
        // rather than a refusal, exactly as for any subtype out of scope.
        _ticketRepository.GetTickets(
                Arg.Any<PagingQuery>(),
                Arg.Any<TicketStatus?>(),
                Arg.Any<IReadOnlyCollection<TicketSubtype>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Ticket>(), PagingResult.Empty(20)));

        SetCurrentUser(UserRole.SeniorModerator);
        var (moderatorTickets, _) = await _service.GetTickets(new PagingQuery(), subtype: subtype);

        moderatorTickets.Should().BeEmpty();
        await _ticketRepository.DidNotReceive().GetTickets(
            Arg.Any<PagingQuery>(),
            Arg.Any<TicketStatus?>(),
            Arg.Any<IReadOnlyCollection<TicketSubtype>?>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());

        // The administrator queue passes the category through to the repository:
        // the request does arrive at moderation, it is only scoped.
        IReadOnlyCollection<TicketSubtype>? capturedSubtypes = null;
        _ticketRepository.GetTickets(
                Arg.Any<PagingQuery>(),
                Arg.Any<TicketStatus?>(),
                Arg.Any<IReadOnlyCollection<TicketSubtype>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Ticket>(), PagingResult.Empty(20)))
            .AndDoes(ci => capturedSubtypes = ci.ArgAt<IReadOnlyCollection<TicketSubtype>?>(2));

        SetCurrentUser(UserRole.Admin);
        await _service.GetTickets(new PagingQuery(), subtype: subtype);

        capturedSubtypes.Should().BeEquivalentTo(new[] { subtype });
    }

    [Fact]
    public async Task FilterTicketSubtypesByCallerRole()
    {
        IReadOnlyCollection<TicketSubtype>? capturedSubtypes = null;
        _ticketRepository.GetTickets(
                Arg.Any<PagingQuery>(),
                Arg.Any<TicketStatus?>(),
                Arg.Any<IReadOnlyCollection<TicketSubtype>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()).Returns((Array.Empty<Ticket>(), PagingResult.Empty(20))).AndDoes(ci => { var subtypes = ci.ArgAt<IReadOnlyCollection<TicketSubtype>?>(2); capturedSubtypes = subtypes; });

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
        await _ticketRepository.DidNotReceive().GetTickets(
                Arg.Any<PagingQuery>(),
                Arg.Any<TicketStatus?>(),
                Arg.Any<IReadOnlyCollection<TicketSubtype>?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnTicketToItsReporterRegardlessOfSubtypeScope()
    {
        // A regular user (below Moderator) can always read their own ticket,
        // even one in an admin-only subtype.
        SetCurrentUser(UserRole.RegularUser);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = _currentUserId,
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await _service.GetTicket(_ticketId);

        result.TicketId.Should().Be(_ticketId);
    }

    [Fact]
    public async Task DenyTicketToNonReporterModeratorOnAdminOnlySubtype()
    {
        // Plain moderator, not the reporter, requesting an admin-only subtype
        // by GUID: 404 (not 403) so the endpoint is not an existence oracle.
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var act = () => _service.GetTicket(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTicketToInScopeModerator()
    {
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.UserComplaint
        };
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await _service.GetTicket(_ticketId);

        result.TicketId.Should().Be(_ticketId);
    }

    [Fact]
    public async Task ReturnTicketOfAnySubtypeToAdmin()
    {
        SetCurrentUser(UserRole.Admin);
        var ticket = new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.Bug
        };
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await _service.GetTicket(_ticketId);

        result.TicketId.Should().Be(_ticketId);
    }

    [Fact]
    public async Task DenyMissingTicketWithNotFound()
    {
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns((Ticket?)null);

        var act = () => _service.GetTicket(_ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PassStatusAndSubtypeFiltersToRepositoryForMyFiledTickets()
    {
        // "Мои обращения" filters are server-side: the service forwards the
        // status and subtype straight to the repository (the reporter owns the
        // tickets, so no subtype visibility gating is applied). The page the
        // caller asked for travels with them.
        Guid capturedUserId = Guid.Empty;
        PagingQuery? capturedQuery = null;
        TicketStatus? capturedStatus = null;
        TicketSubtype? capturedSubtype = null;
        _ticketRepository.GetUserTickets(
                Arg.Any<Guid>(), Arg.Any<PagingQuery>(), Arg.Any<TicketStatus?>(), Arg.Any<TicketSubtype?>(),
                Arg.Any<CancellationToken>()).Returns((Array.Empty<Ticket>(), PagingResult.Empty(20))).AndDoes(ci =>
                {
                    var userId = ci.ArgAt<Guid>(0); var query = ci.ArgAt<PagingQuery>(1); var status = ci.ArgAt<TicketStatus?>(2); var subtype = ci.ArgAt<TicketSubtype?>(3); capturedUserId = userId;
                    capturedQuery = query;
                    capturedStatus = status;
                    capturedSubtype = subtype;
                });

        await _service.GetMyFiledTickets(new PagingQuery { Skip = 40, Take = 20 },
            TicketStatus.Closed, TicketSubtype.Bug);

        capturedUserId.Should().Be(_currentUserId);
        capturedQuery!.Skip.Should().Be(40);
        capturedQuery.Take.Should().Be(20);
        capturedStatus.Should().Be(TicketStatus.Closed);
        capturedSubtype.Should().Be(TicketSubtype.Bug);
    }

    [Fact]
    public async Task ReturnTicketByTrackingToken()
    {
        // Guest tracking is token-gated: possession of the token returns the
        // ticket regardless of the caller identity.
        _identityProvider.Current.Returns(Identity.Guest());
        var ticket = new Ticket { TicketId = _ticketId, Status = TicketStatus.WaitingForUser };
        _ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await _service.GetTicketByTrackingToken("tok123");

        result!.TicketId.Should().Be(_ticketId);
    }

    [Fact]
    public async Task ReturnNullForUnknownTrackingToken()
    {
        _ticketRepository.GetByTrackingToken(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);

        var result = await _service.GetTicketByTrackingToken("nope");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateTrackingTokenForGuestIntakeOnly()
    {
        // Guests get an unguessable one-time tracking token; authenticated
        // authors get none (they use "Мои обращения").
        CreateTicketEntity? capturedEntity = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateTicketEntity>(0);
                capturedEntity = e;
            });

        _identityProvider.Current.Returns(Identity.Guest());
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

    /// <summary>
    /// An open ticket of the given reporter, as the repository reads it, and a
    /// closing without a word that goes through.
    /// </summary>
    private void TicketAnsweredBy(Guid? reporterUserId)
    {
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = reporterUserId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.UserComplaint
        });
        ClosingSucceeds();
    }

    /// <summary>
    /// The conditional closing goes through, as it does when nobody closed the
    /// ticket in the meantime.
    /// </summary>
    private void ClosingSucceeds() =>
        _ticketRepository.Close(Arg.Any<Guid>(), Arg.Any<TicketStatus>(), Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

    /// <summary>
    /// The answer of moderation and the status it chose land together: the
    /// comment is written with the ticket moved to that status, and an answer
    /// that chose none waits for the user.
    /// </summary>
    [Theory]
    [InlineData(null, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.WaitingForUser, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.Closed, TicketStatus.Closed)]
    [InlineData(TicketStatus.Spam, TicketStatus.Spam)]
    public async Task WriteTheAnswerOfModerationWithTheStatusItChose(TicketStatus? chosen, TicketStatus expected)
    {
        TicketAnsweredBy(Guid.NewGuid());
        var written = CommentsTaken(out var statusAfter);

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Разобрались", Status = chosen });

        statusAfter().Should().Be(expected);
        written().AuthorId.Should().Be(_currentUserId);
        written().Text.Should().Be("Разобрались");
        await _ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);
    }

    /// <summary>
    /// A closing without a word only moves the status: nothing is written into the
    /// conversation, and the ticket names the moderator who closed it, by the
    /// conditional write of an open ticket.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.Closed, "")]
    [InlineData(TicketStatus.Spam, "   ")]
    public async Task CloseWithoutAWordWhenTheAnswerHasNone(TicketStatus status, string text)
    {
        TicketAnsweredBy(Guid.NewGuid());

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = text, Status = status });

        await _ticketRepository.Received(1).Close(_ticketId, status, _currentUserId, _now, Arg.Any<CancellationToken>());
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The page reads the ticket back as it stands after the answer: closed, it
    /// names who closed it and offers neither the form nor the choice of a status.
    /// </summary>
    [Fact]
    public async Task ReadTheTicketBackAfterTheAnswer()
    {
        var reporter = Guid.NewGuid();
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(
            new Ticket
            {
                TicketId = _ticketId,
                ReporterUserId = reporter,
                Status = TicketStatus.WaitingForModeration,
                Subtype = TicketSubtype.UserComplaint
            },
            new Ticket
            {
                TicketId = _ticketId,
                ReporterUserId = reporter,
                Status = TicketStatus.Closed,
                Subtype = TicketSubtype.UserComplaint,
                ResolvedByUsername = "CurrentUser"
            });
        ClosingSucceeds();

        var page = await _service.CreateComment(_ticketId, new CreateTicketComment { Status = TicketStatus.Closed });

        page.TicketId.Should().Be(_ticketId);
        page.Status.Should().Be(TicketStatus.Closed);
        page.ResolvedByUsername.Should().Be("CurrentUser");
        page.CanComment.Should().BeFalse();
        page.CanResolve.Should().BeFalse();
    }

    /// <summary>
    /// The author of a closed ticket learns of the closing from a notification,
    /// once: an answer that closes the ticket is the closing, and its last word
    /// stands on the page the notification leads to.
    /// </summary>
    [Theory]
    [InlineData("Предупреждение выдано, спасибо")]
    [InlineData("")]
    public async Task AnnounceTheClosingToTheAuthorOfTheTicket(string text)
    {
        TicketAnsweredBy(Guid.NewGuid());
        CommentsTaken();

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = text, Status = TicketStatus.Closed });

        await _eventProducer.Received(1).SendAsync(EventType.TicketResolved, _ticketId);
        await _eventProducer.DidNotReceive().SendAsync(EventType.TicketAnswered, Arg.Any<Guid>());
    }

    /// <summary>
    /// A guest has no inbox: his tracking page is where the answer appears.
    /// </summary>
    [Theory]
    [InlineData("Доступ восстановлен")]
    [InlineData("")]
    public async Task AnnounceNoClosingToAGuest(string text)
    {
        TicketAnsweredBy(null);
        CommentsTaken();

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = text, Status = TicketStatus.Closed });

        await _eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    /// <summary>
    /// Spam is closed without telling whoever sent it, with a word or without.
    /// </summary>
    [Theory]
    [InlineData("Реклама")]
    [InlineData("")]
    public async Task AnnounceNothingAboutSpam(string text)
    {
        TicketAnsweredBy(Guid.NewGuid());
        CommentsTaken();

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = text, Status = TicketStatus.Spam });

        await _eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    /// <summary>
    /// Of two closings without a word sent at once, the second closes nothing and
    /// its moderator is told so; nobody is announced anything twice.
    /// </summary>
    [Fact]
    public async Task RefuseAClosingThatLostTheRace()
    {
        TicketAnsweredBy(Guid.NewGuid());
        _ticketRepository.Close(Arg.Any<Guid>(), Arg.Any<TicketStatus>(), Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => _service.CreateComment(_ticketId, new CreateTicketComment { Status = TicketStatus.Closed });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest && e.Message.Contains("уже закрыто"));
        await _eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    /// <summary>
    /// A ticket read closed is not closed again, with a word or without.
    /// </summary>
    [Theory]
    [InlineData("", "уже закрыто")]
    [InlineData("И еще одно", "Обращение закрыто")]
    public async Task RefuseToCloseAClosedTicket(string text, string refusal)
    {
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = Guid.NewGuid(),
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.UserComplaint
        });

        var act = () => _service.CreateComment(_ticketId,
            new CreateTicketComment { Text = text, Status = TicketStatus.Spam });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest && e.Message.Contains(refusal));
        await _ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// A comment is stored with its tag names the way the site writes them, by
    /// the tag set of the editor it is written in: the comments' one, [mod]
    /// included.
    /// </summary>
    [Fact]
    public async Task StoreTheCommentWithTheTagNamesOfTheSite()
    {
        var tagNames = Substitute.For<IBbTagNameCase>();
        tagNames.ToLowerCase("[MOD]Разобрались[/MOD]", BbTagSet.WithModBlock).Returns("[mod]Разобрались[/mod]");
        var service = new TicketService(
            _createValidator,
            _createIntakeValidator,
            _createCommentValidator,
            _ticketRepository,
            _userLookupService,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            _eventProducer,
            tagNames);
        var captured = OpenTicketTakingComments(Guid.NewGuid());

        await service.CreateComment(_ticketId, new CreateTicketComment { Text = "[MOD]Разобрались[/MOD]" });

        captured().Text.Should().Be("[mod]Разобрались[/mod]");
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
        _userLookupService.FindUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, _targetUserId));
        _userLookupService.GetAsync(_targetUserId)
            .Returns(new GeneralUser { UserId = _targetUserId, Username = "TargetUser" });
        CreateTicketEntity? captured = null;
        _ticketRepository.Create(Arg.Any<CreateTicketEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ticket())
            .AndDoes(ci => captured = ci.ArgAt<CreateTicketEntity>(0));

        await _service.CreateIntakeTicket(new CreateTicketIntake
        {
            Subtype = subtype,
            Text = "Не согласен",
            TargetUsername = subtype == TicketSubtype.UserComplaint ? "TargetUser" : null,
            ViolationUrl = "https://dm.am/warnings?username=CurrentUser"
        });

        captured.Should().NotBeNull();
        captured!.Description.Should().EndWith($"{caption}: https://dm.am/warnings?username=CurrentUser");
    }

    /// <summary>
    /// An open ticket of a user complaint, filed by the given reporter, that the
    /// repository takes a comment into; the entity it was given is read back by
    /// the function returned.
    /// </summary>
    private Func<CreateTicketCommentEntity> OpenTicketTakingComments(Guid? reporterUserId,
        TicketSubtype subtype = TicketSubtype.UserComplaint, Guid? targetUserId = null)
    {
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = reporterUserId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = subtype,
            TargetUserId = targetUserId
        });
        return CommentsTaken();
    }

    /// <summary>
    /// The identifiers the factory hands out from here on are the comment's.
    /// </summary>
    private void CommentIdentifiers()
    {
        _guidFactory.Create().Returns(_commentId);
    }

    /// <summary>
    /// The repository takes the comment it is given; the entity and the status it
    /// was asked to move the ticket to are read back by the function returned.
    /// </summary>
    private Func<CreateTicketCommentEntity> CommentsTaken() => CommentsTaken(out _);

    private Func<CreateTicketCommentEntity> CommentsTaken(out Func<TicketStatus> statusAfter)
    {
        CommentIdentifiers();
        CreateTicketCommentEntity? captured = null;
        TicketStatus? status = null;
        _ticketRepository.AddComment(Arg.Any<CreateTicketCommentEntity>(), Arg.Any<TicketStatus>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => (TicketCommentOutcome.Added, new TicketComment
            {
                TicketCommentId = ci.ArgAt<CreateTicketCommentEntity>(0).TicketCommentId,
                TicketId = _ticketId,
                Text = ci.ArgAt<CreateTicketCommentEntity>(0).Text
            }))
            .AndDoes(ci =>
            {
                captured = ci.ArgAt<CreateTicketCommentEntity>(0);
                status = ci.ArgAt<TicketStatus>(1);
            });
        statusAfter = () => status ?? throw new InvalidOperationException("nothing was written");
        return () => captured ?? throw new InvalidOperationException("nothing was written");
    }

    /// <summary>
    /// The author's comment leaves the ticket waiting for moderation, a moderator's
    /// waiting for the author: the state says whose move it is.
    /// </summary>
    [Theory]
    [InlineData(true, TicketStatus.WaitingForModeration)]
    [InlineData(false, TicketStatus.WaitingForUser)]
    public async Task PassTheTurnWithAComment(bool byAuthor, TicketStatus expected)
    {
        SetCurrentUser(byAuthor ? UserRole.RegularUser : UserRole.Moderator);
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = byAuthor ? _currentUserId : Guid.NewGuid(),
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint
        });
        var written = CommentsTaken(out var statusAfter);

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Ответ" });

        statusAfter().Should().Be(expected);
        written().AuthorId.Should().Be(_currentUserId);
        written().TicketId.Should().Be(_ticketId);
    }

    /// <summary>
    /// A moderator who filed a ticket is its side: his comment is a reply of the
    /// author, not an answer of moderation.
    /// </summary>
    [Fact]
    public async Task TakeTheCommentOfAModeratorWhoFiledTheTicketAsAReply()
    {
        SetCurrentUser(UserRole.SeniorModerator);
        OpenTicketTakingComments(_currentUserId);
        CommentsTaken(out var statusAfter);

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Дополню" });

        statusAfter().Should().Be(TicketStatus.WaitingForModeration);
        await _eventProducer.Received(1).SendAsync(EventType.TicketReplied, _commentId);
        await _eventProducer.DidNotReceive().SendAsync(EventType.TicketAnswered, Arg.Any<Guid>());
    }

    /// <summary>
    /// The guest writes by the token, signed by nobody, and his comment waits for
    /// moderation; whoever holds the token, the text loses [mod] as a guest's does.
    /// </summary>
    [Fact]
    public async Task WriteTheCommentOfTheTokenAsTheGuests()
    {
        SetCurrentUser(UserRole.Admin);
        _ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.AccessRecovery,
            IsGuest = true
        });
        var written = CommentsTaken(out var statusAfter);

        await _service.CreateTrackedComment("tok123",
            new CreateTicketComment { Text = "Вот [mod]для модерации[/mod] почта" });

        written().AuthorId.Should().BeNull("the comment of the token is the guest's, whoever is signed in");
        written().Text.Should().Be("Вот для модерации почта",
            "the block of a guest is unwrapped, as every guest text is");
        statusAfter().Should().Be(TicketStatus.WaitingForModeration);
        await _eventProducer.Received(1).SendAsync(EventType.TicketReplied, _commentId);
    }

    [Fact]
    public async Task AnswerAnUnknownTokenWithNotFound()
    {
        _ticketRepository.GetByTrackingToken(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Ticket?)null);

        var act = () => _service.CreateTrackedComment("nope", new CreateTicketComment { Text = "Есть кто?" });

        await act.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// [mod] stays in the comment of a moderator, and the comment of a user loses
    /// the block and keeps its words, as in every comment of the site.
    /// </summary>
    [Theory]
    [InlineData(UserRole.RegularUser, "Текст служебное")]
    [InlineData(UserRole.Moderator, "Текст [mod]служебное[/mod]")]
    public async Task KeepTheModBlockOfAModeratorOnly(UserRole role, string stored)
    {
        SetCurrentUser(role);
        var written = OpenTicketTakingComments(role == UserRole.RegularUser ? _currentUserId : Guid.NewGuid());

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Текст [mod]служебное[/mod]" });

        written().Text.Should().Be(stored);
    }

    /// <summary>
    /// A ban that silences discussions does not silence the author in his own
    /// ticket: the ticket is how a sanctioned user reaches moderation.
    /// </summary>
    [Fact]
    public async Task LetTheAuthorUnderADemocraticBanWrite()
    {
        var identity = Identity.Success(
            new AuthenticatedUser
            {
                UserId = _currentUserId,
                Role = UserRole.RegularUser,
                Username = "CurrentUser",
                AccessPolicy = AccessPolicy.DemocraticBan
            },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(identity);
        var written = OpenTicketTakingComments(_currentUserId);

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Прошу пересмотреть" });

        written().AuthorId.Should().Be(_currentUserId);
    }

    /// <summary>
    /// Whoever neither filed a ticket nor moderates it cannot tell it from a
    /// ticket that does not exist, to read or to write.
    /// </summary>
    [Theory]
    [InlineData(UserRole.RegularUser, TicketSubtype.UserComplaint)]
    [InlineData(UserRole.Moderator, TicketSubtype.AccessRecovery)]
    [InlineData(UserRole.SeniorModerator, TicketSubtype.PersonalData)]
    public async Task AnswerWhoeverMayNotReadTheTicketAsIfThereWereNone(UserRole role, TicketSubtype subtype)
    {
        SetCurrentUser(role);
        OpenTicketTakingComments(Guid.NewGuid(), subtype);

        var read = () => _service.GetTicket(_ticketId);
        var write = () => _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Я тут" });

        await read.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await write.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The moderator a complaint names as the violator is an ordinary user to it:
    /// he does not read it, write in it or close it, and another moderator of his
    /// role does.
    /// </summary>
    [Fact]
    public async Task KeepAComplaintAwayFromTheModeratorItNames()
    {
        SetCurrentUser(UserRole.Moderator);
        OpenTicketTakingComments(Guid.NewGuid(), targetUserId: _currentUserId);

        var read = () => _service.GetTicket(_ticketId);
        var write = () => _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Это неправда" });
        var close = () => _service.CreateComment(_ticketId, new CreateTicketComment { Status = TicketStatus.Spam });

        await read.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await write.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await close.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
        await _ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);

        OpenTicketTakingComments(Guid.NewGuid(), targetUserId: Guid.NewGuid());
        (await _service.GetTicket(_ticketId)).CanResolve.Should().BeTrue(
            "a complaint about somebody else is his to moderate");
    }

    /// <summary>
    /// The queue and its counters hold what the page of a ticket opens: the caller
    /// is passed down as the violator whose complaints are left out.
    /// </summary>
    [Fact]
    public async Task LeaveTheComplaintsAboutTheCallerOutOfHisQueue()
    {
        SetCurrentUser(UserRole.Moderator);
        Guid? listed = null;
        Guid? counted = null;
        _ticketRepository.GetTickets(Arg.Any<PagingQuery>(), Arg.Any<TicketStatus?>(),
                Arg.Any<IReadOnlyCollection<TicketSubtype>?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Ticket>(), PagingResult.Empty(20)))
            .AndDoes(ci => listed = ci.ArgAt<Guid?>(3));
        _ticketRepository.GetTicketCounts(Arg.Any<IReadOnlyCollection<TicketSubtype>?>(), Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<TicketStatus, int>())
            .AndDoes(ci => counted = ci.ArgAt<Guid?>(1));

        await _service.GetTickets(new PagingQuery(), subtype: TicketSubtype.UserComplaint);
        await _service.GetTicketStats();

        listed.Should().Be(_currentUserId);
        counted.Should().Be(_currentUserId);
    }

    /// <summary>
    /// The author of a ticket chooses no status, even when his role would let him:
    /// a moderator who filed a complaint does not close it as spam in the name of
    /// moderation. The answer that asks for one is refused whole, text included,
    /// rather than written without the status it asked for.
    /// </summary>
    [Theory]
    [InlineData(UserRole.SeniorModerator, TicketStatus.Spam, "")]
    [InlineData(UserRole.SeniorModerator, TicketStatus.Closed, "Закрываю")]
    [InlineData(UserRole.RegularUser, TicketStatus.Closed, "Вопрос решен")]
    [InlineData(UserRole.RegularUser, TicketStatus.WaitingForUser, "Жду ответа")]
    public async Task RefuseAStatusFromTheAuthor(UserRole role, TicketStatus status, string text)
    {
        SetCurrentUser(role);
        OpenTicketTakingComments(_currentUserId);
        ClosingSucceeds();

        var page = await _service.GetTicket(_ticketId);
        var answer = () => _service.CreateComment(_ticketId, new CreateTicketComment { Text = text, Status = status });

        page.CanResolve.Should().BeFalse();
        page.CanComment.Should().BeTrue();
        await answer.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The holder of the token answers as the guest who filed the ticket, and the
    /// author chooses no status.
    /// </summary>
    [Fact]
    public async Task RefuseAStatusFromTheHolderOfTheToken()
    {
        _ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.AccessRecovery,
            IsGuest = true
        });
        CommentsTaken();

        var act = () => _service.CreateTrackedComment("tok123",
            new CreateTicketComment { Text = "Все, спасибо", Status = TicketStatus.Closed });

        await act.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The answer of moderation is news to an author with an account; a guest
    /// reads it on his tracking page, and nobody is told of a comment refused.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnnounceTheAnswerOfModerationToAnAuthorWithAnAccount(bool hasAccount)
    {
        OpenTicketTakingComments(hasAccount ? Guid.NewGuid() : null);

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Пришлите ссылку" });

        if (hasAccount)
        {
            await _eventProducer.Received(1).SendAsync(EventType.TicketAnswered, _commentId);
        }
        else
        {
            await _eventProducer.DidNotReceive().SendAsync(EventType.TicketAnswered, Arg.Any<Guid>());
        }

        await _eventProducer.DidNotReceive().SendAsync(EventType.TicketReplied, Arg.Any<Guid>());
    }

    /// <summary>
    /// The repository decides whether the comment lands: a ticket closed beside
    /// the comment and a conversation at its ceiling both refuse it, and nothing
    /// is announced.
    /// </summary>
    [Theory]
    [InlineData(TicketCommentOutcome.Closed, "Обращение закрыто")]
    [InlineData(TicketCommentOutcome.Full, "100 ответов")]
    public async Task RefuseACommentTheRepositoryDidNotTake(TicketCommentOutcome outcome, string refusal)
    {
        OpenTicketTakingComments(Guid.NewGuid());
        _ticketRepository.AddComment(Arg.Any<CreateTicketCommentEntity>(), Arg.Any<TicketStatus>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((outcome, (TicketComment?)null));

        var act = () => _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Еще одно" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest && e.Message.Contains(refusal));
        await _eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    /// <summary>
    /// The ceiling the repository holds is the hundred of the conversation.
    /// </summary>
    [Fact]
    public async Task HoldTheConversationToAHundredComments()
    {
        OpenTicketTakingComments(Guid.NewGuid());

        await _service.CreateComment(_ticketId, new CreateTicketComment { Text = "Ответ" });

        await _ticketRepository.Received(1).AddComment(Arg.Any<CreateTicketCommentEntity>(),
            Arg.Any<TicketStatus>(), 100, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A closed ticket takes no comment, and the form is not offered on it; a full
    /// conversation offers no form either.
    /// </summary>
    [Fact]
    public async Task OfferTheFormOnlyWhereACommentLands()
    {
        _ticketRepository.Get(_ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            ReporterUserId = _currentUserId,
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.UserComplaint
        });

        (await _service.GetTicket(_ticketId)).CanComment.Should().BeFalse();
        var act = () => _service.CreateComment(_ticketId, new CreateTicketComment { Text = "А еще" });
        await act.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.BadRequest);
        await _ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);

        OpenTicketTakingComments(_currentUserId);
        _ticketRepository.GetComments(_ticketId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, 100).Select(_ => new TicketComment()).ToArray());
        (await _service.GetTicket(_ticketId)).CanComment.Should().BeFalse();
    }

    /// <summary>
    /// The guest page offers what the guest may do and nothing of moderation's,
    /// whoever is signed in while reading it.
    /// </summary>
    [Fact]
    public async Task OfferTheHolderOfTheTokenTheFormAndNoClosing()
    {
        SetCurrentUser(UserRole.Admin);
        _ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = _ticketId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.UserComplaint,
            TargetUserId = _targetUserId
        });

        var page = await _service.GetTicketByTrackingToken("tok123");

        page!.CanComment.Should().BeTrue();
        page.CanResolve.Should().BeFalse();
    }
}
