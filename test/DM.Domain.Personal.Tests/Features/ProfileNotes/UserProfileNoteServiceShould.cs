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
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.ProfileNotes;

public class UserProfileNoteServiceShould : UnitTestBase
{
    private readonly IUserProfileNoteRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
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
        _identityProvider.Current.Returns(identity);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_noteId);

        var validator = Mock<IValidator<CreateUserProfileNote>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<CreateUserProfileNote>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new UserProfileNoteService(
            _repository,
            _userRepository,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            validator);
    }

    [Fact]
    /// <summary>
    /// An anonymous viewer has no note of their own, and that is an answer.
    /// </summary>
    /// <remarks>
    /// This used to throw, and the profile page caught the exception to learn
    /// that its viewer was not signed in — control flow across a layer boundary,
    /// where the nullable return already said the same thing. The exception was
    /// also one the error middleware does not map, so any other caller would
    /// have met it as 500.
    /// </remarks>
    public async Task ReturnNothingWhenGettingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        _identityProvider.Current.Returns(guestIdentity);

        var note = await _service.GetNote("Subject");

        note.Should().BeNull();
    }

    [Fact]
    public async Task ReturnNullWhenGettingNoteForNonexistentUser()
    {
        _userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        var result = await _service.GetNote("Unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNoteSuccessfully()
    {
        var note = new UserProfileNote { Id = _noteId };

        _userRepository.FindUserIdAsync("Subject").Returns(_subjectUserId);
        _repository.Get(_currentUserId, _subjectUserId, Arg.Any<CancellationToken>()).Returns(note);

        var result = await _service.GetNote("Subject");

        result.Should().Be(note);
    }

    [Fact]
    public async Task ThrowWhenUpsertingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        _identityProvider.Current.Returns(guestIdentity);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Note" };
        var act = () => _service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThrowWhenUpsertingNoteForNonexistentUser()
    {
        _userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Unknown", Text = "Note" };
        var act = () => _service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound)
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task ThrowWhenCreatingNoteAboutYourself()
    {
        _userRepository.FindUserIdAsync("CurrentUser").Returns(_currentUserId);

        var createNote = new CreateUserProfileNote { SubjectUsername = "CurrentUser", Text = "Note" };
        var act = () => _service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage("Нельзя оставить заметку о себе");
    }

    [Fact]
    public async Task DeleteNoteWhenUpsertingWithEmptyText()
    {
        var existingNote = new UserProfileNote { Id = _noteId };

        _userRepository.FindUserIdAsync("Subject").Returns(_subjectUserId);
        _repository.Get(_currentUserId, _subjectUserId, Arg.Any<CancellationToken>()).Returns(existingNote);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "" };
        var result = await _service.UpsertNote(createNote);

        result.Should().BeNull();
        await _repository.Received(1).Delete(_noteId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNewNoteWhenNoneExists()
    {
        _userRepository.FindUserIdAsync("Subject").Returns(_subjectUserId);
        _repository.Get(_currentUserId, _subjectUserId, Arg.Any<CancellationToken>()).Returns((UserProfileNote?)null);

        CreateUserProfileNoteEntity? capturedEntity = null;
        _repository.Create(Arg.Any<CreateUserProfileNoteEntity>(), Arg.Any<CancellationToken>())
            .Returns(new UserProfileNote())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateUserProfileNoteEntity>(0);
                capturedEntity = e;
            });

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
        var existingNote = new UserProfileNote { Id = _noteId };

        _userRepository.FindUserIdAsync("Subject").Returns(_subjectUserId);
        _repository.Get(_currentUserId, _subjectUserId, Arg.Any<CancellationToken>()).Returns(existingNote);

        UpdateUserProfileNoteEntity? capturedEntity = null;
        _repository.Update(Arg.Any<UpdateUserProfileNoteEntity>(), Arg.Any<CancellationToken>())
            .Returns(existingNote)
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateUserProfileNoteEntity>(0);
                capturedEntity = e;
            });

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
        var note = new UserProfileNote { Id = _noteId };

        _userRepository.FindUserIdAsync("Subject").Returns(_subjectUserId);
        _repository.Get(_currentUserId, _subjectUserId, Arg.Any<CancellationToken>()).Returns(note);

        await _service.DeleteNote("Subject");

        await _repository.Received(1).Delete(_noteId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenDeletingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        _identityProvider.Current.Returns(guestIdentity);

        var act = () => _service.DeleteNote("Subject");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }
}
