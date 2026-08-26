using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
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
using DM.Testing;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Notepads;

public class BlogNotepadServiceShould : UnitTestBase
{
    private readonly INotepadRepository _repository;
    private readonly IBlogService _blogService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly BlogNotepadService _service;

    public BlogNotepadServiceShould()
    {
        _repository = Mock<INotepadRepository>();
        _blogService = Mock<IBlogService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _identityProvider.Current.Returns(Identity.Guest());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new BlogNotepadService(
            _repository,
            _blogService,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider);
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
        var identity = AuthenticatedIdentities.Of(userId);
        var entries = new List<NotepadEntry> { new() { Id = Guid.NewGuid() } };

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntriesAsync(NotepadType.Blog, blogId, null, default).Returns(entries);

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
        var identity = AuthenticatedIdentities.Of(assistantId);
        var entries = new List<NotepadEntry> { new() { Id = Guid.NewGuid() } };

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntriesAsync(NotepadType.Blog, blogId, null, default).Returns(entries);

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
        var identity = AuthenticatedIdentities.Of(nonParticipantId);

        _identityProvider.Current.Returns(identity);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);

        var act = async () => await _service.GetEntries(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThrowWhenEntryNotFound()
    {
        var entryId = Guid.NewGuid();
        _repository.GetEntryAsync(entryId, default).Returns((NotepadEntry?)null);

        var act = async () => await _service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowWhenAccessingNonBlogEntry()
    {
        var entryId = Guid.NewGuid();
        var entry = new NotepadEntry { Id = entryId, NotepadType = NotepadType.Player };

        _repository.GetEntryAsync(entryId, default).Returns(entry);

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
        var identity = AuthenticatedIdentities.Of(userId);
        var createEntry = new CreateNotepadEntry { Title = "Test", Content = "Content" };
        var createdEntry = new NotepadEntry { Id = entryId };

        _identityProvider.Current.Returns(identity);
        _guidFactory.Create().Returns(entryId);
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), default).Returns(createdEntry);

        var result = await _service.CreateEntry(blogId, createEntry);

        result.Id.Should().Be(entryId);
        await _repository.Received(1).CreateEntryAsync(
            Arg.Is<CreateNotepadEntryInternal>(e =>
                e.NotepadType == NotepadType.Blog &&
                e.ContainerId == blogId &&
                e.AuthorId == userId),
            default);
    }

    [Fact]
    public async Task LetTheAuthorOfAnEntryEditIt()
    {
        var blogId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogWithAssistant(blogId, Guid.NewGuid(), assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(assistantId));
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntryAsync(entryId, default).Returns(entry);
        _repository.UpdateEntryAsync(Arg.Any<UpdateNotepadEntryInternal>(), default).Returns(entry);

        var result = await _service.UpdateEntry(entryId, new UpdateNotepadEntry { Title = "Updated Entry" });

        result.Should().BeSameAs(entry);
    }

    [Fact]
    public async Task RefuseToLetAnAssistantEditSomebodyElsesEntry()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogWithAssistant(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, ownerId);

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(assistantId));
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await _service.UpdateEntry(entryId, new UpdateNotepadEntry { Title = "Updated Entry" });

        // The notepad is open to an assistant, the words in it are not theirs to
        // rewrite: the entry would still stand under the owner's name.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().UpdateEntryAsync(
            Arg.Any<UpdateNotepadEntryInternal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToLetTheBlogOwnerEditAnEntryTheyDidNotWrite()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogWithAssistant(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(ownerId));
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await _service.UpdateEntry(entryId, new UpdateNotepadEntry { Title = "Updated Entry" });

        // Owning the blog is the right to remove an entry, not to rewrite one.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LetTheBlogOwnerDeleteAnEntryWrittenBySomebodyElse()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogWithAssistant(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(ownerId));
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntryAsync(entryId, default).Returns(entry);

        await _service.DeleteEntry(entryId);

        await _repository.Received(1).DeleteEntryAsync(entryId, ownerId, default);
    }

    [Fact]
    public async Task RefuseToLetAnAssistantDeleteSomebodyElsesEntry()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogWithAssistant(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, ownerId);

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(assistantId));
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await _service.DeleteEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().DeleteEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LetTheAuthorDeleteTheirOwnEntry()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogWithAssistant(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        _identityProvider.Current.Returns(AuthenticatedIdentities.Of(assistantId));
        _blogService.GetBlogAsync(blogId, default).Returns(blog);
        _repository.GetEntryAsync(entryId, default).Returns(entry);

        await _service.DeleteEntry(entryId);

        await _repository.Received(1).DeleteEntryAsync(entryId, assistantId, default);
    }

    private static BlogDto BlogWithAssistant(Guid blogId, Guid ownerId, Guid assistantId) => new()
    {
        Id = blogId,
        Author = new GeneralUser { UserId = ownerId },
        Assistants = new List<BlogAssistantInfo> { new() { UserId = assistantId } }
    };

    private static NotepadEntry BlogEntry(Guid entryId, Guid blogId, Guid authorId) => new()
    {
        Id = entryId,
        NotepadType = NotepadType.Blog,
        ContainerId = blogId,
        AuthorId = authorId
    };

}
