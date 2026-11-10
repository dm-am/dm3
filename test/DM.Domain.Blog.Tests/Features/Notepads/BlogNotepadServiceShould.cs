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
using DM.Domain.Core.Dto;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Domain.Account.Features.Authentication;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Notepads;

public class BlogNotepadServiceShould : UnitTestBase
{
    private readonly INotepadRepository repository;
    private readonly IBlogService blogService;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly BlogNotepadService service;

    public BlogNotepadServiceShould()
    {
        repository = Mock<INotepadRepository>();
        blogService = Mock<IBlogService>();
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        identityProvider.Current.Returns(Identity.Guest());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new BlogNotepadService(
            repository,
            blogService,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Fact]
    public async Task AllowBlogOwnerToGetEntries()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = BlogOf(blogId, userId);
        var identity = Identities.User(userId, "testuser");
        var entries = new List<NotepadEntry> { new() { Id = Guid.NewGuid() } };

        identityProvider.Current.Returns(identity);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntriesAsync(NotepadType.Blog, blogId, null, default).Returns(entries);

        var result = await service.GetEntries(blogId);

        result.Should().BeEquivalentTo(entries);
    }

    [Fact]
    public async Task AllowAssistantToGetEntries()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var blog = BlogOf(blogId, ownerId, assistantId);
        var identity = Identities.User(assistantId, "testuser");
        var entries = new List<NotepadEntry> { new() { Id = Guid.NewGuid() } };

        identityProvider.Current.Returns(identity);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntriesAsync(NotepadType.Blog, blogId, null, default).Returns(entries);

        var result = await service.GetEntries(blogId);

        result.Should().BeEquivalentTo(entries);
    }

    [Fact]
    public async Task ThrowWhenNonParticipantAccessesNotepad()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var nonParticipantId = Guid.NewGuid();
        var blog = BlogOf(blogId, ownerId);
        var identity = Identities.User(nonParticipantId, "testuser");

        identityProvider.Current.Returns(identity);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        blogService.IsVisibleToViewerAsync(blogId, default).Returns(true);

        var act = async () => await service.GetEntries(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    // A blog the reader may not even open answers as a blog that is not there: a 403 on its notepad
    // would tell him it exists.
    [Fact]
    public async Task AnswerAStrangerOnTheNotepadOfAHiddenBlogAsOnNoBlogAtAll()
    {
        var blogId = Guid.NewGuid();
        var blog = BlogOf(blogId, Guid.NewGuid());

        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        blogService.IsVisibleToViewerAsync(blogId, default).Returns(false);

        var act = async () => await service.GetEntries(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.BlogNotFound);
    }

    [Fact]
    public async Task ThrowWhenEntryNotFound()
    {
        var entryId = Guid.NewGuid();
        repository.GetEntryAsync(entryId, default).Returns((NotepadEntry?)null);

        var act = async () => await service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowWhenAccessingNonBlogEntry()
    {
        var entryId = Guid.NewGuid();
        var entry = new NotepadEntry { Id = entryId, NotepadType = NotepadType.Player };

        repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await service.GetEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEntryWithCorrectData()
    {
        var blogId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blog = BlogOf(blogId, userId);
        var identity = Identities.User(userId, "testuser");
        var createEntry = new CreateNotepadEntry { Title = "Test", Content = "Content" };
        var createdEntry = new NotepadEntry { Id = entryId };

        identityProvider.Current.Returns(identity);
        guidFactory.Create().Returns(entryId);
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.CreateEntryAsync(Arg.Any<CreateNotepadEntryInternal>(), default).Returns(createdEntry);

        var result = await service.CreateEntry(blogId, createEntry);

        result.Id.Should().Be(entryId);
        await repository.Received(1).CreateEntryAsync(
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
        var blog = BlogOf(blogId, Guid.NewGuid(), assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        identityProvider.Current.Returns(Identities.User(assistantId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntryAsync(entryId, default).Returns(entry);
        repository.UpdateEntryAsync(Arg.Any<UpdateNotepadEntryInternal>(), default).Returns(entry);

        var result = await service.UpdateEntry(entryId, new UpdateNotepadEntry { Title = "Updated Entry" });

        result.Should().BeSameAs(entry);
    }

    [Fact]
    public async Task RefuseToLetAnAssistantEditSomebodyElsesEntry()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogOf(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, ownerId);

        identityProvider.Current.Returns(Identities.User(assistantId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await service.UpdateEntry(entryId, new UpdateNotepadEntry { Title = "Updated Entry" });

        // The notepad is open to an assistant, the words in it are not theirs to
        // rewrite: the entry would still stand under the owner's name.
        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().UpdateEntryAsync(
            Arg.Any<UpdateNotepadEntryInternal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToLetTheBlogOwnerEditAnEntryTheyDidNotWrite()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogOf(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        identityProvider.Current.Returns(Identities.User(ownerId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await service.UpdateEntry(entryId, new UpdateNotepadEntry { Title = "Updated Entry" });

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
        var blog = BlogOf(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        identityProvider.Current.Returns(Identities.User(ownerId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntryAsync(entryId, default).Returns(entry);

        await service.DeleteEntry(entryId);

        await repository.Received(1).DeleteEntryAsync(entryId, ownerId, default);
    }

    [Fact]
    public async Task RefuseToLetAnAssistantDeleteSomebodyElsesEntry()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogOf(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, ownerId);

        identityProvider.Current.Returns(Identities.User(assistantId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntryAsync(entryId, default).Returns(entry);

        var act = async () => await service.DeleteEntry(entryId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().DeleteEntryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LetTheAuthorDeleteTheirOwnEntry()
    {
        var blogId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var blog = BlogOf(blogId, ownerId, assistantId);
        var entry = BlogEntry(entryId, blogId, assistantId);

        identityProvider.Current.Returns(Identities.User(assistantId, "testuser"));
        blogService.GetUnfiltered(blogId, default).Returns(blog);
        repository.GetEntryAsync(entryId, default).Returns(entry);

        await service.DeleteEntry(entryId);

        await repository.Received(1).DeleteEntryAsync(entryId, assistantId, default);
    }

    private static BlogDto BlogOf(Guid blogId, Guid ownerId, params Guid[] assistantIds) => new()
    {
        Id = blogId,
        Author = new GeneralUser { UserId = ownerId },
        Assistants = assistantIds.Select(id => new BlogAssistantInfo { UserId = id }).ToList()
    };

    private static NotepadEntry BlogEntry(Guid entryId, Guid blogId, Guid authorId) => new()
    {
        Id = entryId,
        NotepadType = NotepadType.Blog,
        ContainerId = blogId,
        AuthorId = authorId
    };
}
