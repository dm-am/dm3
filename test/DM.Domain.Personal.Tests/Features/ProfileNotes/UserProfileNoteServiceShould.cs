using DM.Domain.Core.Exceptions;
using System.Net;
using FluentValidation.Results;
using FluentValidation;
using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.ProfileNotes;

public class UserProfileNoteServiceShould : UnitTestBase
{
    private readonly Mock<IUserProfileNoteRepository> _repository;
    private readonly Mock<IUserRepository> _userRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly UserProfileNoteService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _subjectUserId = Guid.NewGuid();
    private readonly Guid _noteId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public UserProfileNoteServiceShould()
    {
        _repository = Mock<IUserProfileNoteRepository>();
        _userRepository = Mock<IUserRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identities.User(_currentUserId, "CurrentUser", UserRole.RegularUser);
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_noteId);

        var validator = Mock<IValidator<CreateUserProfileNote>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateUserProfileNote>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _service = new UserProfileNoteService(
            _repository.Object,
            _userRepository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            validator.Object);
    }

    [Fact]
    public async Task ThrowWhenGettingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        _identityProvider.Setup(p => p.Current).Returns(guestIdentity);

        var act = () => _service.GetNote("Subject");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Authentication required");
    }

    [Fact]
    public async Task ReturnNullWhenGettingNoteForNonexistentUser()
    {
        _userRepository.Setup(r => r.GetUserAsync("Unknown"))
            .ReturnsAsync((GeneralUser?)null);

        var result = await _service.GetNote("Unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNoteSuccessfully()
    {
        var subjectUser = new GeneralUser { UserId = _subjectUserId, Username = "Subject" };
        var note = new UserProfileNote { Id = _noteId };

        _userRepository.Setup(r => r.GetUserAsync("Subject")).ReturnsAsync(subjectUser);
        _repository.Setup(r => r.Get(_currentUserId, _subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await _service.GetNote("Subject");

        result.Should().Be(note);
    }

    [Fact]
    public async Task ThrowWhenUpsertingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        _identityProvider.Setup(p => p.Current).Returns(guestIdentity);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Note" };
        var act = () => _service.UpsertNote(createNote);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Authentication required");
    }

    [Fact]
    public async Task ThrowWhenUpsertingNoteForNonexistentUser()
    {
        _userRepository.Setup(r => r.GetUserAsync("Unknown"))
            .ReturnsAsync((GeneralUser?)null);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Unknown", Text = "Note" };
        var act = () => _service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound)
            .Where(e => e.Message.Contains("not found"));
    }

    [Fact]
    public async Task ThrowWhenCreatingNoteAboutYourself()
    {
        var currentUser = new GeneralUser { UserId = _currentUserId, Username = "CurrentUser" };
        _userRepository.Setup(r => r.GetUserAsync("CurrentUser")).ReturnsAsync(currentUser);

        var createNote = new CreateUserProfileNote { SubjectUsername = "CurrentUser", Text = "Note" };
        var act = () => _service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage("Cannot create a note about yourself");
    }

    [Fact]
    public async Task DeleteNoteWhenUpsertingWithEmptyText()
    {
        var subjectUser = new GeneralUser { UserId = _subjectUserId, Username = "Subject" };
        var existingNote = new UserProfileNote { Id = _noteId };

        _userRepository.Setup(r => r.GetUserAsync("Subject")).ReturnsAsync(subjectUser);
        _repository.Setup(r => r.Get(_currentUserId, _subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingNote);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "" };
        var result = await _service.UpsertNote(createNote);

        result.Should().BeNull();
        _repository.Verify(r => r.Delete(_noteId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateNewNoteWhenNoneExists()
    {
        var subjectUser = new GeneralUser { UserId = _subjectUserId, Username = "Subject" };
        _userRepository.Setup(r => r.GetUserAsync("Subject")).ReturnsAsync(subjectUser);
        _repository.Setup(r => r.Get(_currentUserId, _subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfileNote?)null);

        CreateUserProfileNoteEntity? capturedEntity = null;
        _repository.Setup(r => r.Create(It.IsAny<CreateUserProfileNoteEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreateUserProfileNoteEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new UserProfileNote());

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Note text" };
        await _service.UpsertNote(createNote);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Id.Should().Be(_noteId);
        capturedEntity.OwnerId.Should().Be(_currentUserId);
        capturedEntity.SubjectUserId.Should().Be(_subjectUserId);
        capturedEntity.Text.Should().Be("Note text");
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task UpdateExistingNote()
    {
        var subjectUser = new GeneralUser { UserId = _subjectUserId, Username = "Subject" };
        var existingNote = new UserProfileNote { Id = _noteId };

        _userRepository.Setup(r => r.GetUserAsync("Subject")).ReturnsAsync(subjectUser);
        _repository.Setup(r => r.Get(_currentUserId, _subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingNote);

        UpdateUserProfileNoteEntity? capturedEntity = null;
        _repository.Setup(r => r.Update(It.IsAny<UpdateUserProfileNoteEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateUserProfileNoteEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(existingNote);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Updated text" };
        await _service.UpsertNote(createNote);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Id.Should().Be(_noteId);
        capturedEntity.Text.Should().Be("Updated text");
        capturedEntity.ModifiedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task DeleteNoteSuccessfully()
    {
        var subjectUser = new GeneralUser { UserId = _subjectUserId, Username = "Subject" };
        var note = new UserProfileNote { Id = _noteId };

        _userRepository.Setup(r => r.GetUserAsync("Subject")).ReturnsAsync(subjectUser);
        _repository.Setup(r => r.Get(_currentUserId, _subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        await _service.DeleteNote("Subject");

        _repository.Verify(r => r.Delete(_noteId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenDeletingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        _identityProvider.Setup(p => p.Current).Returns(guestIdentity);

        var act = () => _service.DeleteNote("Subject");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Authentication required");
    }
}
