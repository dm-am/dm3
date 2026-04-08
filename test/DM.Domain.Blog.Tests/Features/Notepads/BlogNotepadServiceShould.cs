using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Notepads;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Notepads;
using DM.Domain.Core.Users;
using DM.Domain.Core.Dto;
using DM.Domain.Account.Features.Authentication;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Notepads;

public class BlogNotepadServiceShould : UnitTestBase
{
    private readonly Mock<INotepadRepository> _repository;
    private readonly Mock<IBlogService> _blogService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly BlogNotepadService _service;

    public BlogNotepadServiceShould()
    {
        _repository = Mock<INotepadRepository>();
        _blogService = Mock<IBlogService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new BlogNotepadService(
            _repository.Object,
            _blogService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public async Task AllowBlogOwnerToGetEntries()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = userId },
            Assistants = new List<BlogAssistantInfo>()
        };
        var identity = CreateAuthenticatedIdentity(userId);
        var entries = new List<NotepadEntry> { new() { Id = Guid.NewGuid() } };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.Blog, blogId, null, default)).ReturnsAsync(entries);

        var result = await _service.GetEntries(blogId);

        result.Should().BeEquivalentTo(entries);
    }

    [Fact]
    public async Task AllowAssistantToGetEntries()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = ownerId },
            Assistants = new List<BlogAssistantInfo> { new() { UserId = assistantId } }
        };
        var identity = CreateAuthenticatedIdentity(assistantId);
        var entries = new List<NotepadEntry> { new() { Id = Guid.NewGuid() } };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.GetEntriesAsync(NotepadType.Blog, blogId, null, default)).ReturnsAsync(entries);

        var result = await _service.GetEntries(blogId);

        result.Should().BeEquivalentTo(entries);
    }

    [Fact]
    public async Task ThrowWhenNonParticipantAccessesNotepad()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var nonParticipantId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = ownerId },
            Assistants = new List<BlogAssistantInfo>()
        };
        var identity = CreateAuthenticatedIdentity(nonParticipantId);

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);

        var act = async () => await _service.GetEntries(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowWhenEntryNotFound()
    {
        var entryId = Guid.NewGuid();
        _repository.Setup(r => r.GetEntryAsync(entryId, default)).ReturnsAsync((NotepadEntry?)null);

        var act = async () => await _service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowWhenAccessingNonBlogEntry()
    {
        var entryId = Guid.NewGuid();
        var entry = new NotepadEntry { Id = entryId, NotepadType = NotepadType.Player };

        _repository.Setup(r => r.GetEntryAsync(entryId, default)).ReturnsAsync(entry);

        var act = async () => await _service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEntryWithCorrectData()
    {
        var blogId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = new BlogDto
        {
            Id = blogId,
            Author = new GeneralUser { UserId = userId },
            Assistants = new List<BlogAssistantInfo>()
        };
        var identity = CreateAuthenticatedIdentity(userId);
        var createEntry = new CreateNotepadEntry { Title = "Test", Content = "Content" };
        var createdEntry = new NotepadEntry { Id = entryId };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _guidFactory.Setup(f => f.Create()).Returns(entryId);
        _blogService.Setup(s => s.GetBlogAsync(blogId, default)).ReturnsAsync(blog);
        _repository.Setup(r => r.CreateEntryAsync(It.IsAny<CreateNotepadEntryInternal>(), default))
            .ReturnsAsync(createdEntry);

        var result = await _service.CreateEntry(blogId, createEntry);

        result.Id.Should().Be(entryId);
        _repository.Verify(r => r.CreateEntryAsync(
            It.Is<CreateNotepadEntryInternal>(e =>
                e.NotepadType == NotepadType.Blog &&
                e.ContainerId == blogId &&
                e.AuthorId == userId),
            default), Times.Once);
    }

    private static IIdentity CreateAuthenticatedIdentity(Guid userId)
    {
        var user = new AuthenticatedUser { UserId = userId, Username = "testuser" };
        var session = new Session();
        return Identity.Success(user, session, UserSettings.Default, "token");
    }
}
