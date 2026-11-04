using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Notepads;
using DM.Domain.Personal.Features.Notepads;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notepads;

public class UserNotepadServiceShould : UnitTestBase
{
    private readonly INotepadRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly UserNotepadService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly Guid entryId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public UserNotepadServiceShould()
    {
        repository = Mock<INotepadRepository>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identities.User(currentUserId, "CurrentUser", UserRole.RegularUser);
        identityProvider.Current.Returns(identity);
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(entryId);

        service = new UserNotepadService(
            repository,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public async Task GetEntriesForCurrentUser()
    {
        repository.GetEntriesAsync(NotepadType.User, currentUserId, null, Arg.Any<CancellationToken>()).Returns([]);

        await service.GetEntries();

        await repository.Received(1).GetEntriesAsync(NotepadType.User, currentUserId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenGettingNonexistentEntry()
    {
        repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns((NotepadEntry?)null);

        var act = () => service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдена"));
    }

    [Fact]
    public async Task ThrowWhenAccessingEntryBelongingToAnotherUser()
    {
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.User,
            ContainerId = Guid.NewGuid()
        };
        repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);

        var act = () => service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Недостаточно прав"));
    }

    [Fact]
    public async Task CreateEntryWithCorrectData()
    {
        CreateNotepadEntryInternal? capturedEntity = null;
        repository.CreateEntryAsync(Arg.Do<CreateNotepadEntryInternal>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new NotepadEntry());

        var createEntry = new CreateNotepadEntry
        {
            Title = "Test Entry",
            Content = "Test Content"
        };

        await service.CreateEntry(createEntry);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(entryId);
        capturedEntity.NotepadType.Should().Be(NotepadType.User);
        capturedEntity.ContainerId.Should().Be(currentUserId);
        capturedEntity.AuthorId.Should().Be(currentUserId);
        capturedEntity.Title.Should().Be("Test Entry");
        capturedEntity.Content.Should().Be("Test Content");
        capturedEntity.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public async Task UpdateEntrySuccessfully()
    {
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.User,
            ContainerId = currentUserId
        };
        repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);

        UpdateNotepadEntryInternal? capturedEntity = null;
        repository.UpdateEntryAsync(Arg.Do<UpdateNotepadEntryInternal>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(entry);

        var updateEntry = new UpdateNotepadEntry
        {
            Title = "Updated Title",
            Content = "Updated Content",
            SortOrder = 5
        };

        await service.UpdateEntry(entryId, updateEntry);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(entryId);
        capturedEntity.Title.Should().Be("Updated Title");
        capturedEntity.Content.Should().Be("Updated Content");
        capturedEntity.ModifiedUtc.Should().Be(now);
    }

    [Fact]
    public async Task DeleteEntrySuccessfully()
    {
        var entry = new NotepadEntry
        {
            Id = entryId,
            NotepadType = NotepadType.User,
            ContainerId = currentUserId
        };
        repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns(entry);

        await service.DeleteEntry(entryId);

        await repository.Received(1).DeleteEntryAsync(entryId, currentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoNothingWhenDeletingNonexistentEntry()
    {
        repository.GetEntryAsync(entryId, Arg.Any<CancellationToken>()).Returns((NotepadEntry?)null);

        await service.DeleteEntry(entryId);

        await repository.DidNotReceive().DeleteEntryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
