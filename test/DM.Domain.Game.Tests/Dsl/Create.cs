using System;

namespace DM.Domain.Game.Tests.Dsl;

public static class Create
{
    public static AuthenticatedUserBuilder User(Guid userId) => new(userId);
    public static AuthenticatedUserBuilder User() => new(Guid.NewGuid());
    public static GameBuilder Game() => new();
}
