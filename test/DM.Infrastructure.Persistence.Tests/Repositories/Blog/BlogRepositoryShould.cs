using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Blog;

public class BlogRepositoryShould : UnitTestBase
{
    private readonly DmDbContext _dbContext;
    private readonly BlogRepository _repository;
    private readonly Mock<IPublicIdService> _publicIdService;
    private readonly Guid _authorId = Guid.NewGuid();

    public BlogRepositoryShould()
    {
        _dbContext = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BlogMappingProfile>();
            cfg.AddProfile<GeneralUserMappingProfile>();
        }).CreateMapper();

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        // The EF Core InMemory provider does not generate the identity
        // SerialNumber (Postgres does), so the encode is exercised with a
        // stubbed service. The real serial→5-letter encoding is covered by
        // PublicIdServiceShould; this test verifies the repository wiring:
        // CreateBlog encodes the serial via IPublicIdService and surfaces the
        // result as PublicId, exactly like GameRepository.
        _publicIdService = Mock<IPublicIdService>();

        _repository = new BlogRepository(_dbContext, mapper, dateTimeProvider.Object, _publicIdService.Object);
    }

    private async Task SeedAuthorAsync()
    {
        _dbContext.Users.Add(new DbUser
        {
            UserId = _authorId,
            Username = "author",
            Email = "author@example.com",
            PasswordHash = "hash",
            Salt = "salt"
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task EncodePublicIdFromSerialNumberOnCreate()
    {
        await SeedAuthorAsync();
        _publicIdService.Setup(s => s.Encode(It.IsAny<int>())).Returns("qrstuv");

        var blogId = Guid.NewGuid();
        var blog = await _repository.CreateBlog(new CreateBlogEntity
        {
            BlogId = blogId,
            OwnerId = _authorId,
            Title = "Test Blog",
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow
        });

        // The DTO surfaces the encoded public id...
        blog.PublicId.Should().Be("qrstuv");
        // ...and it is persisted on the entity, encoded from the serial number.
        var stored = await _dbContext.Blogs.FirstAsync(b => b.BlogId == blogId);
        stored.PublicId.Should().Be("qrstuv");
        _publicIdService.Verify(s => s.Encode(stored.SerialNumber), Times.Once);
    }

    [Fact]
    public async Task AssignSortOrderByPositionWhenReordering()
    {
        var blogId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        _dbContext.Rubrics.AddRange(
            new DbRubric { RubricId = first, BlogId = blogId, Title = "A", SortOrder = 0 },
            new DbRubric { RubricId = second, BlogId = blogId, Title = "B", SortOrder = 1 },
            new DbRubric { RubricId = third, BlogId = blogId, Title = "C", SortOrder = 2 });
        await _dbContext.SaveChangesAsync();

        // Reverse the display order.
        await _repository.ReorderRubrics(blogId, new[] { third, second, first });

        (await _dbContext.Rubrics.FindAsync(third))!.SortOrder.Should().Be(0);
        (await _dbContext.Rubrics.FindAsync(second))!.SortOrder.Should().Be(1);
        (await _dbContext.Rubrics.FindAsync(first))!.SortOrder.Should().Be(2);
    }

    public override void Dispose()
    {
        _dbContext.Dispose();
        base.Dispose();
    }
}
