using System;

namespace DM.Testing.Dsl;

public static class Create
{
    public static AuthenticatedUserBuilder User(Guid userId) => new(userId);
    public static AuthenticatedUserBuilder User() => new(Guid.NewGuid());
}
