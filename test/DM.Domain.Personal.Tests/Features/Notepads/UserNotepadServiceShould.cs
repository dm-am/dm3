using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Notepads;
using DM.Domain.Personal.Features.Notepads;
using DM.Domain.Personal.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notepads;

public class UserNotepadServiceShould : UnitTestBase
{
    private readonly Mock<INotepadRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly UserNotepadService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _entryId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public UserNotepadServiceShould()
    {
        _repository = Mock<INotepadRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        var identity = Identity.Authenticated(_currentUserId, "CurrentUser", UserRole.RegularUser);
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_entryId);

        _service = new UserNotepadService(
            _repository.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task GetEntriesForCurrentUser()
    {
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.User, _currentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.GetEntries();

        _repository.Verify(r => r.GetEntriesAsync(NotepadType.User, _currentUserId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenGettingNonexistentEntry()
    {
        _repository.Setup(r => r.GetEntryAsync(_entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotepadEntry?)null);

        var act = () => _service.GetEntry(_entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("not found"));
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
        _repository.Setup(r => r.GetEntryAsync(_entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var act = () => _service.GetEntry(_entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Access denied"));
    }

    [Fact]
    public async Task CreateEntryWithCorrectData()
    {
        CreateNotepadEntryInternal? capturedEntity = null;
        _repository.Setup(r => r.CreateEntryAsync(It.IsAny<CreateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .Callback<CreateNotepadEntryInternal, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new NotepadEntry());

        var createEntry = new CreateNotepadEntry
        {
            CategoryId = _categoryId,
            Title = "Test Entry",
            Content = "Test Content"
        };

        await _service.CreateEntry(createEntry);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EntryId.Should().Be(_entryId);
        capturedEntity.NotepadType.Should().Be(NotepadType.User);
        capturedEntity.ContainerId.Should().Be(_currentUserId);
        capturedEntity.AuthorId.Should().Be(_currentUserId);
        capturedEntity.CategoryId.Should().Be(_categoryId);
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
        _repository.Setup(r => r.GetEntryAsync(_entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        UpdateNotepadEntryInternal? capturedEntity = null;
        _repository.Setup(r => r.UpdateEntryAsync(It.IsAny<UpdateNotepadEntryInternal>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateNotepadEntryInternal, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(entry);

        var updateEntry = new UpdateNotepadEntry
        {
            Title = "Updated Title",
            Content = "Updated Content",
            CategoryId = _categoryId,
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
        _repository.Setup(r => r.GetEntryAsync(_entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        await _service.DeleteEntry(_entryId);

        _repository.Verify(r => r.DeleteEntryAsync(_entryId, _currentUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoNothingWhenDeletingNonexistentEntry()
    {
        _repository.Setup(r => r.GetEntryAsync(_entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotepadEntry?)null);

        await _service.DeleteEntry(_entryId);

        _repository.Verify(r => r.DeleteEntryAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCategoriesForCurrentUser()
    {
        _repository.Setup(r => r.GetCategoriesAsync(NotepadType.User, _currentUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.GetCategories();

        _repository.Verify(r => r.GetCategoriesAsync(NotepadType.User, _currentUserId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCategoryWithCorrectData()
    {
        CreateNotepadCategoryInternal? capturedEntity = null;
        _repository.Setup(r => r.CreateCategoryAsync(It.IsAny<CreateNotepadCategoryInternal>(), It.IsAny<CancellationToken>()))
            .Callback<CreateNotepadCategoryInternal, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync(new NotepadCategory());

        var createCategory = new CreateNotepadCategory { Name = "Test Category" };

        await _service.CreateCategory(createCategory);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.CategoryId.Should().Be(_entryId);
        capturedEntity.NotepadType.Should().Be(NotepadType.User);
        capturedEntity.ContainerId.Should().Be(_currentUserId);
        capturedEntity.AuthorId.Should().Be(_currentUserId);
        capturedEntity.Name.Should().Be("Test Category");
        capturedEntity.CreatedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task ThrowWhenAccessingCategoryBelongingToAnotherUser()
    {
        var category = new NotepadCategory
        {
            Id = _categoryId,
            NotepadType = NotepadType.User,
            ContainerId = Guid.NewGuid()
        };
        _repository.Setup(r => r.GetCategoryAsync(_categoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var updateCategory = new UpdateNotepadCategory { Name = "Updated" };
        var act = () => _service.UpdateCategory(_categoryId, updateCategory);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Access denied"));
    }
}
