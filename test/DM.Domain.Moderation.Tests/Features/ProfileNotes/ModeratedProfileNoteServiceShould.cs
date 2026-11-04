using System;
using System.Threading;
using System.Threading.Tasks;
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
using DM.Testing.Dsl;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.ProfileNotes;

public class ModeratedProfileNoteServiceShould : UnitTestBase
{
    private readonly IIdentityProvider identityProvider;
    private readonly IIntentionManager intentionManager;
    private readonly IUserLookupService userLookupService;
    private readonly IModeratedProfileNoteRepository noteRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IGuidFactory guidFactory;
    private readonly ModeratedProfileNoteService service;
    private readonly Guid moderatorUserId = Guid.NewGuid();
    private readonly Guid targetUserId = Guid.NewGuid();
    private readonly Guid noteId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public ModeratedProfileNoteServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        intentionManager = Mock<IIntentionManager>();
        userLookupService = Mock<IUserLookupService>();
        noteRepository = Mock<IModeratedProfileNoteRepository>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        guidFactory = Mock<IGuidFactory>();

        identityProvider.Current.Returns(Identities.User(moderatorUserId, "Moderator", UserRole.Moderator));
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(noteId);

        var createValidator = Mock<IValidator<CreateModeratedProfileNote>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateModeratedProfileNote>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateModeratedProfileNote>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateModeratedProfileNote>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        service = new ModeratedProfileNoteService(
            identityProvider,
            intentionManager,
            userLookupService,
            noteRepository,
            dateTimeProvider,
            guidFactory,
            createValidator,
            updateValidator);
    }

    [Fact]
    public async Task AuthorizeViewModNotesWhenGettingNotes()
    {
        var user = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(user);
        noteRepository.GetNotes(targetUserId).Returns([]);

        await service.GetNotes("Target");

        intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.ViewModNotes);
    }

    [Fact]
    public async Task AuthorizeCreateModNoteWhenCreatingNote()
    {
        var user = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(user);
        noteRepository.Create(Arg.Any<CreateModeratedProfileNoteEntity>()).Returns(new ModeratedProfileNote());

        var createNote = new CreateModeratedProfileNote { Username = "Target", Text = "Note text" };
        await service.Create(createNote);

        intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.CreateModNote);
    }

    [Fact]
    public async Task CreateNoteWithCorrectData()
    {
        var user = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(user);

        CreateModeratedProfileNoteEntity? capturedEntity = null;
        noteRepository.Create(Arg.Do<CreateModeratedProfileNoteEntity>(e => capturedEntity = e))
            .Returns(new ModeratedProfileNote());

        var createNote = new CreateModeratedProfileNote { Username = "Target", Text = "Note text" };
        await service.Create(createNote);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.Id.Should().Be(noteId);
        capturedEntity.UserId.Should().Be(targetUserId);
        capturedEntity.AuthorId.Should().Be(moderatorUserId);
        capturedEntity.Text.Should().Be("Note text");
        capturedEntity.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public async Task ThrowWhenUpdatingNonexistentNote()
    {
        noteRepository.GetNote(noteId).Returns((ModeratedProfileNote?)null);

        var updateNote = new UpdateModeratedProfileNote { Id = noteId, Text = "Updated text" };
        var act = () => service.Update(updateNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдена"));
    }

    [Fact]
    public async Task ThrowWhenNonAuthorTriesToUpdateNote()
    {
        var note = new ModeratedProfileNote
        {
            Id = noteId,
            Author = new GeneralUser { UserId = Guid.NewGuid() },
            User = new GeneralUser { UserId = targetUserId, Username = "Target" }
        };
        noteRepository.GetNote(noteId).Returns(note);
        userLookupService.GetAsync(targetUserId).Returns(note.User);

        var updateNote = new UpdateModeratedProfileNote { Id = noteId, Text = "Updated text" };
        var act = () => service.Update(updateNote);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("только свои заметки"));
    }

    [Fact]
    public async Task AuthorizeDeleteModNoteWhenDeletingNote()
    {
        var note = new ModeratedProfileNote
        {
            Id = noteId,
            Author = new GeneralUser { UserId = moderatorUserId },
            User = new GeneralUser { UserId = targetUserId, Username = "Target" }
        };
        noteRepository.GetNote(noteId).Returns(note);
        userLookupService.GetAsync(targetUserId).Returns(note.User);

        await service.Delete(noteId);

        intentionManager.Received(1).ThrowIfForbidden(ModerationIntention.DeleteModNote);
        await noteRepository.Received(1).Delete(noteId);
    }
}
