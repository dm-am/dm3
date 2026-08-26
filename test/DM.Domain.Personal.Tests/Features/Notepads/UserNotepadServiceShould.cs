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
    private readonly INotepadRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly UserNotepadService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _entryId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public UserNotepadServiceShould()
    {
        _repository = Mock<INotepadRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identities.User(_currentUserId, "CurrentUser", UserRole.RegularUser);
        _identityProvider.Current.Returns(identity);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_entryId);

        _service = new UserNotepadService(
            _repository,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public async Task GetEntriesForCurrentUser()
    {
        _repository.GetEntriesAsync(NotepadType.User, _currentUserId, null, Arg.Any<CancellationToken>()).Returns([]);

        await _service.GetEntries();

        await _repository.Received(1).GetEntriesAsync(NotepadType.User, _currentUserId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenGettingNonexistentEntry()
    {
        _repository.GetEntryAsync(_entryId, Arg.Any<CancellationToken>()).Returns((NotepadEntry?)null);

        var act = () => _service.GetEntry(_entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдена"));
    }

    [Fact]
    public async Task ThrowWhenAccessingEntryBelongingToAnotherUser()
    {
        var entry = new NotepadEntry
        {
            Id = _entryId,
            NotepadType = NotepadType.User,
            ContainerId = Guid.NewGuid()
        };
        _repository.GetEntryAsync(_entryId, Arg.Any<CancellationToken>()).Returns(entry);

        var act = () => _service.GetEntry(_entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Недостаточно прав"));
    }

    [Fact]
    public async Task CreateEntryWithCorrectData()
    {
        CreateNotepadEntryInternal? capturedEntity = null;
        _repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(new NotepadEntry())
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<CreateNotepadEntryInternal>(0);
                capturedEntity = e;
            });

        var createEntry = new CreateNotepadEntry
        {
            Title = "Test Entry",
            Content = "Test Content"
        };

        await _service.CreateEntry(createEntry);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(_entryId);
        capturedEntity.NotepadType.Should().Be(NotepadType.User);
        capturedEntity.ContainerId.Should().Be(_currentUserId);
        capturedEntity.AuthorId.Should().Be(_currentUserId);
        capturedEntity.Title.Should().Be("Test Entry");
        capturedEntity.Content.Should().Be("Test Content");
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task UpdateEntrySuccessfully()
    {
        var entry = new NotepadEntry
        {
            Id = _entryId,
            NotepadType = NotepadType.User,
            ContainerId = _currentUserId
        };
        _repository.GetEntryAsync(_entryId, Arg.Any<CancellationToken>()).Returns(entry);

        UpdateNotepadEntryInternal? capturedEntity = null;
        _repository.UpdateEntryAsync(Arg.Any<UpdateNotepadEntryInternal>(), Arg.Any<CancellationToken>())
            .Returns(entry)
            .AndDoes(ci =>
            {
                var e = ci.ArgAt<UpdateNotepadEntryInternal>(0);
                capturedEntity = e;
            });

        var updateEntry = new UpdateNotepadEntry
        {
            Title = "Updated Title",
            Content = "Updated Content",
            SortOrder = 5
        };

        await _service.UpdateEntry(_entryId, updateEntry);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(_entryId);
        capturedEntity.Title.Should().Be("Updated Title");
        capturedEntity.Content.Should().Be("Updated Content");
        capturedEntity.ModifiedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task DeleteEntrySuccessfully()
    {
        var entry = new NotepadEntry
        {
            Id = _entryId,
            NotepadType = NotepadType.User,
            ContainerId = _currentUserId
        };
        _repository.GetEntryAsync(_entryId, Arg.Any<CancellationToken>()).Returns(entry);

        await _service.DeleteEntry(_entryId);

        await _repository.Received(1).DeleteEntryAsync(_entryId, _currentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoNothingWhenDeletingNonexistentEntry()
    {
        _repository.GetEntryAsync(_entryId, Arg.Any<CancellationToken>()).Returns((NotepadEntry?)null);

        await _service.DeleteEntry(_entryId);

        await _repository.DidNotReceive().DeleteEntryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
