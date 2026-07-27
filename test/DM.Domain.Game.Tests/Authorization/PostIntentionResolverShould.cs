using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

public class PostIntentionResolverShould : UnitTestBase
{
    private readonly PostIntentionResolver _resolver = new();

    private static Post PostBy(Guid authorId) =>
        new() { Author = new GeneralUser { UserId = authorId } };

    [Fact]
    public void AllowDeleteForAuthor()
    {
        var authorId = Guid.NewGuid();
        var user = Create.User(authorId).WithRole(UserRole.RegularUser).Please();

        _resolver.IsAllowed(user, PostIntention.Delete, PostBy(authorId)).Should().BeTrue();
    }

    [Fact]
    public void AllowDeleteForModerator()
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();

        _resolver.IsAllowed(user, PostIntention.Delete, PostBy(Guid.NewGuid())).Should().BeTrue();
    }

    [Fact]
    public void ForbidDeleteForNonAuthorRegularUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();

        _resolver.IsAllowed(user, PostIntention.Delete, PostBy(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void AllowEditTextForModerator()
    {
        var user = Create.User().WithRole(UserRole.Moderator).Please();
        var room = new RoomToUpdate { Game = Create.Game().Please() };

        _resolver.IsAllowed(user, PostIntention.EditText, (PostBy(Guid.NewGuid()), room))
            .Should().BeTrue();
    }

    [Fact]
    public void AllowEditTextForAuthor()
    {
        var authorId = Guid.NewGuid();
        var user = Create.User(authorId).WithRole(UserRole.RegularUser).Please();
        var room = new RoomToUpdate { Game = Create.Game().Please() };

        _resolver.IsAllowed(user, PostIntention.EditText, (PostBy(authorId), room))
            .Should().BeTrue();
    }

    [Fact]
    public void ForbidEditTextForUnrelatedRegularUser()
    {
        var user = Create.User().WithRole(UserRole.RegularUser).Please();
        // A different master; the actor is neither author, moderator, nor lead.
        var room = new RoomToUpdate { Game = Create.Game().WithMaster(Guid.NewGuid()).Please() };

        _resolver.IsAllowed(user, PostIntention.EditText, (PostBy(Guid.NewGuid()), room))
            .Should().BeFalse();
    }
}
