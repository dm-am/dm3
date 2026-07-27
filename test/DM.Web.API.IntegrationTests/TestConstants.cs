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

    // User logins (usernames)
    public const string TestUserLogin = "testuser";
    public const string AdminUserLogin = "admin";
    public const string SecondUserLogin = "seconduser";
    public const string ModeratorUserLogin = "moderator";
    public const string MentorUserLogin = "mentor";
    public const string SeniorModeratorUserLogin = "seniormod";

    // Aliases for usernames (same as logins)
    public const string TestUserUsername = TestUserLogin;
    public const string AdminUserUsername = AdminUserLogin;
    public const string SecondUserUsername = SecondUserLogin;
    public const string ModeratorUserUsername = ModeratorUserLogin;
    public const string MentorUserUsername = MentorUserLogin;
    public const string SeniorModeratorUserUsername = SeniorModeratorUserLogin;

    // Chats
    public static readonly Guid TestChatId = Guid.Parse("00000000-0000-0000-0000-000000000070");

    // Blogs
    public static readonly Guid TestBlogId = Guid.Parse("00000000-0000-0000-0000-000000000080");
    public static readonly Guid SecondBlogId = Guid.Parse("00000000-0000-0000-0000-000000000081");

    // Testimonials (website reviews) for existing users
    public static readonly Guid TestTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000090");
    public static readonly Guid AdminTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000091");
    public static readonly Guid SecondTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000092");
    public static readonly Guid ModeratorTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000093");
    public static readonly Guid MentorTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000094");
    public static readonly Guid SeniorModeratorTestimonialId = Guid.Parse("00000000-0000-0000-0000-000000000095");

    // Additional testimonial users (for pagination/filter testing)
    public static readonly Guid[] TestimonialUserIds =
    [
        Guid.Parse("00000000-0000-0000-0000-0000000000a0"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a2"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a3"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a4"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a5"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a6"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a7"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a8"),
        Guid.Parse("00000000-0000-0000-0000-0000000000a9"),
        Guid.Parse("00000000-0000-0000-0000-0000000000aa"),
        Guid.Parse("00000000-0000-0000-0000-0000000000ab"),
        Guid.Parse("00000000-0000-0000-0000-0000000000ac"),
        Guid.Parse("00000000-0000-0000-0000-0000000000ad"),
        Guid.Parse("00000000-0000-0000-0000-0000000000ae"),
        Guid.Parse("00000000-0000-0000-0000-0000000000af"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c0"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c1"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c2"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c3"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c4"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c5"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c6"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c7"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c8"),
        Guid.Parse("00000000-0000-0000-0000-0000000000c9"),
        Guid.Parse("00000000-0000-0000-0000-0000000000ca"),
        Guid.Parse("00000000-0000-0000-0000-0000000000cb"),
    ];

    public static readonly string[] TestimonialUsernames =
    [
        "Археолог", "Светлана", "DragonSlayer", "Мистик", "Алхимик",
        "Странник", "Валькирия", "Книжник", "Феникс", "Звездочет",
        "Кузнец", "Эльфийка", "Бард", "Следопыт", "Ворон", "Чародейка",
        "Хранитель", "Тень", "Летописец", "Оракул", "Пилигрим", "Волчица",
        "Шут", "Маг", "Охотник", "Жрица", "Воин", "Ведьмак",
    ];

    public static readonly Guid[] AdditionalTestimonialIds =
    [
        Guid.Parse("00000000-0000-0000-0000-0000000000b0"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b1"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b2"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b3"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b4"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b5"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b6"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b7"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b8"),
        Guid.Parse("00000000-0000-0000-0000-0000000000b9"),
        Guid.Parse("00000000-0000-0000-0000-0000000000ba"),
        Guid.Parse("00000000-0000-0000-0000-0000000000bb"),
        Guid.Parse("00000000-0000-0000-0000-0000000000bc"),
        Guid.Parse("00000000-0000-0000-0000-0000000000bd"),
        Guid.Parse("00000000-0000-0000-0000-0000000000be"),
        Guid.Parse("00000000-0000-0000-0000-0000000000bf"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d0"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d1"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d2"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d3"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d4"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d5"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d6"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d7"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d8"),
        Guid.Parse("00000000-0000-0000-0000-0000000000d9"),
        Guid.Parse("00000000-0000-0000-0000-0000000000da"),
        Guid.Parse("00000000-0000-0000-0000-0000000000db"),
    ];
}
