using DM.Domain.Core.Exceptions;
using System.Net;
using FluentValidation.Results;
using FluentValidation;
using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
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
    private readonly IUserProfileNoteRepository repository;
    private readonly IUserRepository userRepository;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly UserProfileNoteService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly Guid subjectUserId = Guid.NewGuid();
    private readonly Guid noteId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public UserProfileNoteServiceShould()
    {
        repository = Mock<IUserProfileNoteRepository>();
        userRepository = Mock<IUserRepository>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identities.User(currentUserId, "CurrentUser", UserRole.RegularUser);
        identityProvider.Current.Returns(identity);
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(noteId);

        var validator = Mock<IValidator<CreateUserProfileNote>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<CreateUserProfileNote>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        service = new UserProfileNoteService(
            repository,
            userRepository,
            identityProvider,
            guidFactory,
            dateTimeProvider,
            validator);
    }

    /// <summary>
    /// An anonymous viewer has no note of their own, and that is an answer.
    /// </summary>
    [Fact]
    public async Task ReturnNothingWhenGettingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        identityProvider.Current.Returns(guestIdentity);

        var note = await service.GetNote("Subject");

        note.Should().BeNull();
    }

    [Fact]
    public async Task ReturnNullWhenGettingNoteForNonexistentUser()
    {
        userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        var result = await service.GetNote("Unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNoteSuccessfully()
    {
        var note = new UserProfileNote { Id = noteId };

        userRepository.FindUserIdAsync("Subject").Returns(subjectUserId);
        repository.Get(currentUserId, subjectUserId, Arg.Any<CancellationToken>()).Returns(note);

        var result = await service.GetNote("Subject");

        result.Should().Be(note);
    }

    [Fact]
    public async Task ThrowWhenUpsertingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        identityProvider.Current.Returns(guestIdentity);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Note" };
        var act = () => service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThrowWhenUpsertingNoteForNonexistentUser()
    {
        userRepository.FindUserIdAsync("Unknown").Returns((Guid?)null);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Unknown", Text = "Note" };
        var act = () => service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound)
            .Where(e => e.Message.Contains("не найден"));
    }

    [Fact]
    public async Task ThrowWhenCreatingNoteAboutYourself()
    {
        userRepository.FindUserIdAsync("CurrentUser").Returns(currentUserId);

        var createNote = new CreateUserProfileNote { SubjectUsername = "CurrentUser", Text = "Note" };
        var act = () => service.UpsertNote(createNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage("Нельзя оставить заметку о себе");
    }

    [Fact]
    public async Task DeleteNoteWhenUpsertingWithEmptyText()
    {
        var existingNote = new UserProfileNote { Id = noteId };

        userRepository.FindUserIdAsync("Subject").Returns(subjectUserId);
        repository.Get(currentUserId, subjectUserId, Arg.Any<CancellationToken>()).Returns(existingNote);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "" };
        var result = await service.UpsertNote(createNote);

        result.Should().BeNull();
        await repository.Received(1).Delete(noteId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNewNoteWhenNoneExists()
    {
        userRepository.FindUserIdAsync("Subject").Returns(subjectUserId);

        CreateUserProfileNoteEntity? capturedEntity = null;
        repository.Create(Arg.Do<CreateUserProfileNoteEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new UserProfileNote());

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Note text" };
        await service.UpsertNote(createNote);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Id.Should().Be(noteId);
        capturedEntity.OwnerId.Should().Be(currentUserId);
        capturedEntity.SubjectUserId.Should().Be(subjectUserId);
        capturedEntity.Text.Should().Be("Note text");
        capturedEntity.CreatedUtc.Should().Be(now);
    }

    /// <summary>
    /// A note the pair already has is updated by the same write that creates a
    /// new one, with no read of the note before it.
    /// </summary>
    /// <remarks>
    /// The write decides new or existing itself, so there is nothing to read first.
    /// </remarks>
    [Fact]
    public async Task UpdateExistingNote()
    {
        var existingNote = new UserProfileNote { Id = Guid.NewGuid(), Text = "Old text", CreatedUtc = now.AddDays(-1) };
        var savedNote = new UserProfileNote
        {
            Id = existingNote.Id,
            Text = "Updated text",
            CreatedUtc = existingNote.CreatedUtc,
            ModifiedUtc = now
        };

        userRepository.FindUserIdAsync("Subject").Returns(subjectUserId);
        repository.Get(currentUserId, subjectUserId, Arg.Any<CancellationToken>()).Returns(existingNote);

        CreateUserProfileNoteEntity? capturedEntity = null;
        repository.Create(Arg.Do<CreateUserProfileNoteEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(savedNote);

        var createNote = new CreateUserProfileNote { SubjectUsername = "Subject", Text = "Updated text" };
        var result = await service.UpsertNote(createNote);

        result.Should().Be(savedNote, "the answer is the note as the write left it");
        capturedEntity.Should().NotBeNull();
        capturedEntity!.OwnerId.Should().Be(currentUserId);
        capturedEntity.SubjectUserId.Should().Be(subjectUserId);
        capturedEntity.Text.Should().Be("Updated text");
        capturedEntity.CreatedUtc.Should().Be(now, "on a note the pair already has this dates the modification");
        await repository.DidNotReceive().Get(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteNoteSuccessfully()
    {
        var note = new UserProfileNote { Id = noteId };

        userRepository.FindUserIdAsync("Subject").Returns(subjectUserId);
        repository.Get(currentUserId, subjectUserId, Arg.Any<CancellationToken>()).Returns(note);

        await service.DeleteNote("Subject");

        await repository.Received(1).Delete(noteId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenDeletingNoteWithoutAuthentication()
    {
        var guestIdentity = Identities.Guest();
        identityProvider.Current.Returns(guestIdentity);

        var act = () => service.DeleteNote("Subject");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }
}
