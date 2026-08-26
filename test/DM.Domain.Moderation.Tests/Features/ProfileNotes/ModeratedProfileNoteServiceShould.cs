using System.Threading;
using FluentValidation.Results;
using FluentValidation;
using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Moderation.Authorization;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.ProfileNotes;

public class ModeratedProfileNoteServiceShould : UnitTestBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserLookupService _userLookupService;
    private readonly IModeratedProfileNoteRepository _noteRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly ModeratedProfileNoteService _service;
    private readonly Guid _moderatorUserId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();
    private readonly Guid _noteId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public ModeratedProfileNoteServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();
        _userLookupService = Mock<IUserLookupService>();
        _noteRepository = Mock<IModeratedProfileNoteRepository>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _guidFactory = Mock<IGuidFactory>();

        var moderatorIdentity = Identity.Success(
            new AuthenticatedUser { UserId = _moderatorUserId, Role = UserRole.Moderator, Username = "Moderator" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(moderatorIdentity);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_noteId);

        var createValidator = Mock<IValidator<CreateModeratedProfileNote>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateModeratedProfileNote>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateModeratedProfileNote>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateModeratedProfileNote>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new ModeratedProfileNoteService(
            _identityProvider,
            _intentionManager,
            _userLookupService,
            _noteRepository,
            _dateTimeProvider,
            _guidFactory,
            createValidator,
            updateValidator);
    }

    [Fact]
    public async Task AuthorizeViewModNotesWhenGettingNotes()
    {
        var user = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(user);
        _noteRepository.GetNotes(_targetUserId).Returns([]);

        await _service.GetNotes("Target");

        _intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.ViewModNotes);
    }

    [Fact]
    public async Task AuthorizeCreateModNoteWhenCreatingNote()
    {
        var user = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(user);
        _noteRepository.Create(Arg.Any<CreateModeratedProfileNoteEntity>()).Returns(new ModeratedProfileNote());

        var createNote = new CreateModeratedProfileNote { Username = "Target", Text = "Note text" };
        await _service.Create(createNote);

        _intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.CreateModNote);
    }

    [Fact]
    public async Task CreateNoteWithCorrectData()
    {
        var user = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.GetAsync("Target").Returns(user);

        CreateModeratedProfileNoteEntity? capturedEntity = null;
        _noteRepository.Create(Arg.Any<CreateModeratedProfileNoteEntity>())
            .Returns(new ModeratedProfileNote())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateModeratedProfileNoteEntity>(0);
                capturedEntity = e;
            });

        var createNote = new CreateModeratedProfileNote { Username = "Target", Text = "Note text" };
        await _service.Create(createNote);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Id.Should().Be(_noteId);
        capturedEntity.UserId.Should().Be(_targetUserId);
        capturedEntity.AuthorId.Should().Be(_moderatorUserId);
        capturedEntity.Text.Should().Be("Note text");
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task ThrowWhenUpdatingNonexistentNote()
    {
        _noteRepository.GetNote(_noteId).Returns((ModeratedProfileNote?)null);

        var updateNote = new UpdateModeratedProfileNote { Id = _noteId, Text = "Updated text" };
        var act = () => _service.Update(updateNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдена"));
    }

    [Fact]
    public async Task ThrowWhenNonAuthorTriesToUpdateNote()
    {
        var note = new ModeratedProfileNote
        {
            Id = _noteId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            User = new GeneralUser { UserId = _targetUserId, Username = "Target" }
        };
        _noteRepository.GetNote(_noteId).Returns(note);
        _userLookupService.GetAsync(_targetUserId).Returns(note.User);

        var updateNote = new UpdateModeratedProfileNote { Id = _noteId, Text = "Updated text" };
        var act = () => _service.Update(updateNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("только свои заметки"));
    }

    [Fact]
    public async Task AuthorizeDeleteModNoteWhenDeletingNote()
    {
        var note = new ModeratedProfileNote
        {
            Id = _noteId,
            Author = new GeneralUser { UserId = _moderatorUserId },
            User = new GeneralUser { UserId = _targetUserId, Username = "Target" }
        };
        _noteRepository.GetNote(_noteId).Returns(note);
        _userLookupService.GetAsync(_targetUserId).Returns(note.User);

        await _service.Delete(_noteId);

        _intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.DeleteModNote);
        await _noteRepository.Received(1).Delete(_noteId);
    }
}
