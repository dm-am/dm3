using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using BlogDto = DM.Domain.Blog.Features.Blogs.Blog;
using DM.Domain.Blog.Features.Subscriptions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Blog.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blogs;

// The people of a blog, read through a real blog service: the reader's own moves ask the one rule
// of who may open a blog, and it lives there.
public class BlogUserServiceShould : BlogServiceTestBase
{
    private readonly IBlogSubscriptionService subscriptionService = Mock<IBlogSubscriptionService>();
    private readonly BlogUserService userService;

    public BlogUserServiceShould()
    {
        userService = new BlogUserService(service, repository, subscriptionService, identityProvider, intentionManager);
    }

    [Fact]
    public async Task ThrowWhenSubscribingToOwnBlog()
    {
        var blogId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var blog = new BlogDto
        {
            Id = blogId,
            DraftVisibility = DraftVisibility.Public,
            Author = new GeneralUser { UserId = userId }
        };

        identityProvider.Current.Returns(Identities.User(userId, "user"));
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(blog);

        var act = async () => await userService.Subscribe(blogId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message.Contains("собственный блог"));
    }

    // On one he may not open they answer as on a blog that is not there, word for word, and change
    // nothing: a refusal of its own would say the blog exists, and a subscription would be a write
    // into a module hidden from its writer.
    [Theory]
    [InlineData("subscribe", ModuleStatus.Draft, PremoderationStatus.Approved)]
    [InlineData("subscribe", ModuleStatus.Active, PremoderationStatus.AwaitingEdits)]
    [InlineData("unsubscribe", ModuleStatus.Draft, PremoderationStatus.Approved)]
    [InlineData("unsubscribe", ModuleStatus.Active, PremoderationStatus.AwaitingEdits)]
    [InlineData("leave", ModuleStatus.Draft, PremoderationStatus.Approved)]
    [InlineData("leave", ModuleStatus.Active, PremoderationStatus.AwaitingEdits)]
    public async Task AnswerTheReadersOwnMovesOnAHiddenBlogAsOnNoBlog(
        string move, ModuleStatus status, PremoderationStatus premoderation)
    {
        var blogId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(strangerId, "stranger"));
        // A private draft or a blog awaiting a verdict, of somebody else, and the
        // caller holds no role that opens either.
        repository.Get(blogId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new BlogDto
        {
            Id = blogId,
            Status = status,
            DraftVisibility = DraftVisibility.Private,
            PremoderationStatus = premoderation,
            Author = new GeneralUser { UserId = Guid.NewGuid() }
        });
        repository.RemoveAssistant(blogId, strangerId, Arg.Any<CancellationToken>()).Returns(true);

        Func<Task> act = move switch
        {
            "subscribe" => () => userService.Subscribe(blogId),
            "unsubscribe" => () => userService.Unsubscribe(blogId),
            _ => () => userService.Leave(blogId)
        };

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.BlogNotFound);
        await subscriptionService.DidNotReceiveWithAnyArgs().SubscribeAsync(default, default);
        await subscriptionService.DidNotReceiveWithAnyArgs().UnsubscribeAsync(default, default);
        await repository.DidNotReceiveWithAnyArgs().RemoveAssistant(default, default, default);
    }
}
