namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Well-known test IDs for integration tests
/// </summary>
public static class TestConstants
{
    // Users
    public static readonly Guid TestUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    public static readonly Guid SecondUserId = Guid.Parse("00000000-0000-0000-0000-000000000012");
    public static readonly Guid ModeratorUserId = Guid.Parse("00000000-0000-0000-0000-000000000013");

    // Boards
    public static readonly Guid TestBoardId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public const string TestBoardTitle = "Test Board";

    // Topics
    public static readonly Guid TestTopicId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    public static readonly Guid SecondTopicId = Guid.Parse("00000000-0000-0000-0000-000000000021");

    // Comments
    public static readonly Guid TestCommentId = Guid.Parse("00000000-0000-0000-0000-000000000030");

    // Games
    public static readonly Guid TestGameId = Guid.Parse("00000000-0000-0000-0000-000000000040");
    public static readonly Guid TestRoomId = Guid.Parse("00000000-0000-0000-0000-000000000041");
    public static readonly Guid SecondGameId = Guid.Parse("00000000-0000-0000-0000-000000000042");

    // Conversations
    public static readonly Guid TestConversationId = Guid.Parse("00000000-0000-0000-0000-000000000050");
    public static readonly Guid TestMessageId = Guid.Parse("00000000-0000-0000-0000-000000000060");

    // User logins
    public const string TestUserLogin = "testuser";
    public const string AdminUserLogin = "admin";
    public const string SecondUserLogin = "seconduser";
    public const string ModeratorUserLogin = "moderator";
}
