using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Blog.Blogs;
using DM.Web.API.Features.Community.Users;
using AwesomeAssertions;
using Xunit;
using DomainBlog = DM.Domain.Blog.Features.Blogs.Blog;

namespace DM.Web.API.Tests.Features.Blog;

/// <summary>
/// The author of a blog is not optional, and the reference answers with them.
/// </summary>
/// <remarks>
/// The mapping used to route the author through a null-tolerant hop into a
/// member declared non-nullable - the shape that turned an authorless game
/// roster line into a 500 on the whole details response. A blog is owned by
/// exactly one user, the column is not nullable and the projection hydrates
/// it, so the tolerance guarded nothing while promising that null would be
/// carried; what it actually did was throw. RMG090 now fails the build on that
/// shape, and these two hold the behaviour the mapping is supposed to have.
/// </remarks>
public class BlogMapperShould : UnitTestBase
{
    private static BlogMapper CreateMapper() =>
        new(new UserMapper(new StubImgproxyUrlBuilder()));

    private sealed class StubImgproxyUrlBuilder : IImgproxyUrlBuilder
    {
        public string BuildSquareThumbnail(string sourceObjectKey, int size) => string.Empty;
    }

    [Fact]
    public void CarryTheAuthorOfABlogReference()
    {
        var author = Author();

        var reference = CreateMapper().ToBlogRef(new DomainBlog
        {
            Id = Guid.NewGuid(),
            Title = "Блог",
            Author = author
        });

        reference.Author.Should().NotBeNull();
        reference.Author.Id.Should().Be(author.UserId);
        reference.Author.Username.Should().Be(author.Username);
    }

    [Fact]
    public void CarryTheAuthorOfAFullBlog()
    {
        var author = Author();

        var blog = CreateMapper().ToBlog(new DomainBlog
        {
            Id = Guid.NewGuid(),
            Title = "Блог",
            Description = "Описание",
            Author = author
        });

        blog.Author.Should().NotBeNull();
        blog.Author.Id.Should().Be(author.UserId);
    }

    private static GeneralUser Author() => new()
    {
        UserId = Guid.NewGuid(),
        Username = "blogger",
        Role = UserRole.RegularUser
    };
}
