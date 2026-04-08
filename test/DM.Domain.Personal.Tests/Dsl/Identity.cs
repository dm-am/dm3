using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Personal.Tests.Dsl;

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

    public static IIdentity Authenticated(Guid userId, string username, UserRole role)
    {
        return new TestIdentity
        {
            User = new AuthenticatedUser { UserId = userId, Username = username, Role = role },
            Settings = UserSettings.Default
        };
    }

    public static IIdentity Authenticated(Guid userId, string username, UserRole role, UserSettings settings)
    {
        return new TestIdentity
        {
            User = new AuthenticatedUser { UserId = userId, Username = username, Role = role },
            Settings = settings
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
