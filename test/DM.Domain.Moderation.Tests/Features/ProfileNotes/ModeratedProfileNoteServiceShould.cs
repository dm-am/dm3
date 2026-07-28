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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.ProfileNotes;

public class ModeratedProfileNoteServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IModeratedProfileNoteRepository> _noteRepository;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
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
        _identityProvider.Setup(p => p.Current).Returns(moderatorIdentity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_noteId);

        var createValidator = Mock<IValidator<CreateModeratedProfileNote>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateModeratedProfileNote>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateModeratedProfileNote>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateModeratedProfileNote>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new ModeratedProfileNoteService(
            _identityProvider.Object,
            _intentionManager.Object,
            _userLookupService.Object,
            _noteRepository.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object,
            createValidator.Object,
            updateValidator.Object);
    }

    [Fact]
    public async Task AuthorizeViewModNotesWhenGettingNotes()
    {
        var user = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(user);
        _noteRepository.Setup(r => r.GetNotes(_targetUserId)).ReturnsAsync([]);

        await _service.GetNotes("Target");

        _intentionManager.Verify(m => m.ThrowIfForbidden(ModerationIntention.ViewModNotes), Times.Once);
    }

    [Fact]
    public async Task AuthorizeCreateModNoteWhenCreatingNote()
    {
        var user = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(user);
        _noteRepository.Setup(r => r.Create(It.IsAny<CreateModeratedProfileNoteEntity>()))
            .ReturnsAsync(new ModeratedProfileNote());

        var createNote = new CreateModeratedProfileNote { Username = "Target", Text = "Note text" };
        await _service.Create(createNote);

        _intentionManager.Verify(m => m.ThrowIfForbidden(ModerationIntention.CreateModNote), Times.Once);
    }

    [Fact]
    public async Task CreateNoteWithCorrectData()
    {
        var user = new GeneralUser { UserId = _targetUserId, Username = "Target" };
        _userLookupService.Setup(s => s.GetAsync("Target")).ReturnsAsync(user);

        CreateModeratedProfileNoteEntity? capturedEntity = null;
        _noteRepository.Setup(r => r.Create(It.IsAny<CreateModeratedProfileNoteEntity>()))
            .Callback<CreateModeratedProfileNoteEntity>(e => capturedEntity = e)
            .ReturnsAsync(new ModeratedProfileNote());

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
        _noteRepository.Setup(r => r.GetNote(_noteId)).ReturnsAsync((ModeratedProfileNote?)null);

        var updateNote = new UpdateModeratedProfileNote { Id = _noteId, Text = "Updated text" };
        var act = () => _service.Update(updateNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("not found"));
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
        _noteRepository.Setup(r => r.GetNote(_noteId)).ReturnsAsync(note);
        _userLookupService.Setup(s => s.GetAsync(_targetUserId)).ReturnsAsync(note.User);

        var updateNote = new UpdateModeratedProfileNote { Id = _noteId, Text = "Updated text" };
        var act = () => _service.Update(updateNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("only edit your own"));
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
        _noteRepository.Setup(r => r.GetNote(_noteId)).ReturnsAsync(note);
        _userLookupService.Setup(s => s.GetAsync(_targetUserId)).ReturnsAsync(note.User);

        await _service.Delete(_noteId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(ModerationIntention.DeleteModNote), Times.Once);
        _noteRepository.Verify(r => r.Delete(_noteId), Times.Once);
    }
}
