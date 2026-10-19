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
    public static readonly Guid MentorUserId = Guid.Parse("00000000-0000-0000-0000-000000000014");
    public static readonly Guid SeniorModeratorUserId = Guid.Parse("00000000-0000-0000-0000-000000000015");

    // Inactive users (for subscription testing)
    public static readonly Guid InactiveUser1Id = Guid.Parse("00000000-0000-0000-0000-000000000016");
    public static readonly Guid InactiveUser2Id = Guid.Parse("00000000-0000-0000-0000-000000000017");
    public const string InactiveUser1Username = "inactiveuser1";
    public const string InactiveUser2Username = "inactiveuser2";

    // Boards
    // Outside the range InitialCreate seeds, which ships the "Общий" board as 0...001.
    public static readonly Guid TestBoardId = Guid.Parse("00000000-0000-0000-0000-0000000000b0");
    public const string TestBoardTitle = "Test Board";

    // Topics
    public static readonly Guid TestTopicId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    public static readonly Guid SecondTopicId = Guid.Parse("00000000-0000-0000-0000-000000000021");

    // Comments
    // Nothing seeds a comment under this id: it stands in the requests a guest
    // is refused before the comment is looked up. A test that needs a comment
    // to exist writes its own.
    public static readonly Guid UnseededCommentId = Guid.Parse("00000000-0000-0000-0000-000000000030");

    // Games
    public static readonly Guid TestGameId = Guid.Parse("00000000-0000-0000-0000-000000000040");
    public static readonly Guid TestRoomId = Guid.Parse("00000000-0000-0000-0000-000000000041");

    /// <summary>
    /// The alias TestGame is reachable by: the fixture derives public ids from the seeding order, TestGame first.
    /// </summary>
    public const string TestGamePublicId = "gamea";

    // Characters (seeded into TestGame so game/room tooltips have content to render)
    public static readonly Guid TestCharacterId = Guid.Parse("00000000-0000-0000-0000-000000000050");
    public static readonly Guid SecondCharacterId = Guid.Parse("00000000-0000-0000-0000-000000000051");
    public const string TestCharacterName = "Арагорн Следопыт";
    public const string SecondCharacterName = "Горим Железный Кулак";

    // Game posts + reviews (seeded so /v1/posts rated-listing is non-empty
    // and tooltip-shape assertions can exercise post.room.game)
    public static readonly Guid TestGamePostId = Guid.Parse("00000000-0000-0000-0000-000000000052");
    public static readonly Guid TestGamePostReviewId = Guid.Parse("00000000-0000-0000-0000-000000000053");
    public static readonly Guid SecondGameId = Guid.Parse("00000000-0000-0000-0000-000000000042");
    public static readonly Guid InitialRecruitmentGameId = Guid.Parse("00000000-0000-0000-0000-000000000043");
    public static readonly Guid SubsequentRecruitmentGameId = Guid.Parse("00000000-0000-0000-0000-000000000044");
    public static readonly Guid ClosedRecruitmentGameId = Guid.Parse("00000000-0000-0000-0000-000000000045");
    public static readonly Guid NoRecruitmentGameId = Guid.Parse("00000000-0000-0000-0000-000000000046");

    // Messages (for direct/group chat testing)
    public static readonly Guid TestMessageId = Guid.Parse("00000000-0000-0000-0000-000000000060");

    /// <summary>The password the accounts a test registers sign in with.</summary>
    public const string Password = "TestPass123ok";

    // User logins (usernames)
    public const string TestUserLogin = "testuser";
    public const string AdminUserLogin = "admin";
    public const string SecondUserLogin = "seconduser";
    public const string ModeratorUserLogin = "moderator";
    public const string MentorUserLogin = "mentor";
    public const string SeniorModeratorUserLogin = "seniormod";

    // Chats
    public static readonly Guid TestChatId = Guid.Parse("00000000-0000-0000-0000-000000000070");

    /// <summary>The alias TestChat is reachable by, as the fixture seeds it.</summary>
    public const string TestChatPublicId = "chata";

    // Blogs
    public static readonly Guid TestBlogId = Guid.Parse("00000000-0000-0000-0000-000000000080");

    /// <summary>
    /// The alias TestBlog is reachable by. The fixture derives it from the seeding
    /// order, and TestBlog is seeded first.
    /// </summary>
    public const string TestBlogPublicId = "bloga";

    public static readonly Guid SecondBlogId = Guid.Parse("00000000-0000-0000-0000-000000000081");

    // Testimonials (website reviews) for existing users
    public static readonly Guid TestTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000090");
    public static readonly Guid AdminTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000091");
    public static readonly Guid SecondTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000092");
    public static readonly Guid ModeratorTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000093");
    public static readonly Guid MentorTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000094");
    public static readonly Guid SeniorModeratorTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000095");

    // Additional testimonial users (for pagination/filter testing)
    public static readonly Guid[] TestimonialUserIds = Ids(0xa0, 0xc0);

    public static readonly string[] TestimonialUsernames =
    [
        "Археолог", "Светлана", "DragonSlayer", "Мистик", "Алхимик",
        "Странник", "Валькирия", "Книжник", "Феникс", "Звездочет",
        "Кузнец", "Эльфийка", "Бард", "Следопыт", "Ворон", "Чародейка",
        "Хранитель", "Тень", "Летописец", "Оракул", "Пилигрим", "Волчица",
        "Шут", "Маг", "Охотник", "Жрица", "Воин", "Ведьмак",
    ];

    public static readonly Guid[] AdditionalTestimonialIds = Ids(0xb0, 0xd0);

    /// <summary>Twenty-eight identifiers: sixteen from the first byte on, twelve from the second.</summary>
    private static Guid[] Ids(int first, int second) => Enumerable.Range(0, 28)
        .Select(i => new Guid($"00000000-0000-0000-0000-0000000000{(i < 16 ? first + i : second + i - 16):x2}"))
        .ToArray();
}
