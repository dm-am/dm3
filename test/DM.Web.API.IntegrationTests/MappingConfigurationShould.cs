using System;
using AutoMapper;
using DM.Domain.Core.Users;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AutoMapperConfigurationProvider = AutoMapper.IConfigurationProvider;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbCommentEdit = DM.Infrastructure.Persistence.Entities.Shared.CommentEdit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DomainComment = DM.Domain.Core.Comments.Comment;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The mapper the application actually runs on: every profile of every
/// registered module, composed by RegisterMapper into one configuration.
/// </summary>
/// <remarks>
/// Per-profile tests in the persistence suite each assert a hand-picked subset,
/// so a map whose two profiles live in different modules is covered by none of
/// them. This asserts the composed whole instead, which also means a profile
/// added to any module is covered the moment it is registered.
///
/// What the assertion buys, concretely: a destination member no source member
/// resolves to fails the build. That is the compiler check that mapping by name
/// gives up — <see cref="UserFilter" /> is filled from the query string by name,
/// so a field renamed on one side alone would otherwise stop filtering and say
/// nothing about it.
/// </remarks>
public class MappingConfigurationShould : IntegrationTestBase
{
    public MappingConfigurationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public void BeValidForEveryRegisteredProfile()
    {
        var configuration = DatabaseFixture.Factory.Services
            .GetRequiredService<AutoMapperConfigurationProvider>();

        configuration.AssertConfigurationIsValid();
    }

[Fact]
    public void KeepTheEditTimestampWhenMappingAComment()
    {
        var mapper = DatabaseFixture.Factory.Services.GetRequiredService<IMapper>();
        var edited = DateTimeOffset.UtcNow;
        var entity = new DbComment
        {
            CommentId = Guid.NewGuid(),
            EntityId = Guid.NewGuid(),
            Text = "text",
            CreatedUtc = edited.AddHours(-1),
            Author = new DbUser { Username = "author" },
            Edits =
            [
                new DbCommentEdit { EditedUtc = edited.AddMinutes(-10) },
                new DbCommentEdit { EditedUtc = edited },
            ],
        };

        // The pair had two maps and this half only existed in one of them, so
        // whether an edited comment reported the edit at all came down to profile
        // ordering.
        mapper.Map<DomainComment>(entity).ModifiedUtc.Should().Be(edited);
    }
}