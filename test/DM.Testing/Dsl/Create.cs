using System;

namespace DM.Testing.Dsl;

/// <summary>
/// Entry point for the shared test builders.
/// </summary>
public static class Create
{
    public static AuthenticatedUserBuilder User(Guid userId) => new(userId);
    public static AuthenticatedUserBuilder User() => new(Guid.NewGuid());
}
