using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Community.Tests.Dsl;

public static class Identity
{
    public static IIdentity Guest()
    {
        return new TestIdentity
        {
            User = AuthenticatedUser.Guest,
            Settings = UserSettings.Default
        };
    }

    public static IIdentity User(Guid userId, UserRole role = UserRole.RegularUser)
    {
        return new TestIdentity
        {
            User = new AuthenticatedUser { UserId = userId, Role = role, Username = "TestUser" },
            Settings = UserSettings.Default
        };
    }

    public static IIdentity User(Guid userId, string username, UserRole role = UserRole.RegularUser)
    {
        return new TestIdentity
        {
            User = new AuthenticatedUser { UserId = userId, Role = role, Username = username },
            Settings = UserSettings.Default
        };
    }

    private class TestIdentity : IIdentity
    {
        public AuthenticatedUser User { get; set; } = null!;
        public Session? Session { get; set; }
        public UserSettings Settings { get; set; } = null!;
        public AuthenticationError Error { get; set; }
        public string? AuthenticationToken { get; set; }
    }
}
