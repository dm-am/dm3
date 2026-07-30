using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Testing.Dsl;

/// <summary>
/// Builds an <see cref="IIdentity"/> for a test.
/// </summary>
public static class Identities
{
    public static IIdentity Guest() => new TestIdentity
    {
        User = AuthenticatedUser.Guest,
        Settings = UserSettings.Default
    };

    public static IIdentity User(Guid userId, UserRole role = UserRole.RegularUser) =>
        User(userId, "TestUser", role);

    public static IIdentity User(Guid userId, string username, UserRole role = UserRole.RegularUser) =>
        User(userId, username, role, UserSettings.Default);

    public static IIdentity User(Guid userId, string username, UserRole role, UserSettings settings) =>
        new TestIdentity
        {
            User = new AuthenticatedUser { UserId = userId, Username = username, Role = role },
            Settings = settings
        };

    private class TestIdentity : IIdentity
    {
        public AuthenticatedUser User { get; set; } = null!;
        public Session? Session { get; set; }
        public UserSettings Settings { get; set; } = null!;
        public AuthenticationError Error { get; set; }
        public string? AuthenticationToken { get; set; }
    }
}
