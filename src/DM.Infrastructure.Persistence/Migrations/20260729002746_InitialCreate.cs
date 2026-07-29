using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "AchievementCategories",
                columns: table => new
                {
                    AchievementCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IconName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Metric = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementCategories", x => x.AchievementCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "AwardTypes",
                columns: table => new
                {
                    AwardTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IconName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwardTypes", x => x.AwardTypeId);
                });

            migrationBuilder.CreateTable(
                name: "ContestSeries",
                columns: table => new
                {
                    ContestSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestType = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TopicUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestSeries", x => x.ContestSeriesId);
                });

            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                columns: table => new
                {
                    PendingRegistrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Salt = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHashVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TokenCreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedRules = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.PendingRegistrationId);
                });

            migrationBuilder.CreateTable(
                name: "PeriodDigestTopics",
                columns: table => new
                {
                    PeriodDigestTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: true),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodDigestTopics", x => x.PeriodDigestTopicId);
                });

            migrationBuilder.CreateTable(
                name: "TagGroups",
                columns: table => new
                {
                    TagGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagGroups", x => x.TagGroupId);
                });

            migrationBuilder.CreateTable(
                name: "AchievementTypes",
                columns: table => new
                {
                    AchievementTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: true),
                    AchievementCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementTypes", x => x.AchievementTypeId);
                    table.ForeignKey(
                        name: "FK_AchievementTypes_AchievementCategories_AchievementCategoryId",
                        column: x => x.AchievementCategoryId,
                        principalTable: "AchievementCategories",
                        principalColumn: "AchievementCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortId = table.Column<int>(type: "integer", nullable: false),
                    TagGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.TagId);
                    table.ForeignKey(
                        name: "FK_Tags_TagGroups_TagGroupId",
                        column: x => x.TagGroupId,
                        principalTable: "TagGroups",
                        principalColumn: "TagGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bans",
                columns: table => new
                {
                    BanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    AccessRestrictionPolicy = table.Column<int>(type: "integer", nullable: false),
                    IsVoluntary = table.Column<bool>(type: "boolean", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    LiftedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiftedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LiftReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bans", x => x.BanId);
                });

            migrationBuilder.CreateTable(
                name: "BlogAssistants",
                columns: table => new
                {
                    BlogAssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogAssistants", x => x.BlogAssistantId);
                });

            migrationBuilder.CreateTable(
                name: "BlogBlacklists",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogBlacklists", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "Blogs",
                columns: table => new
                {
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedReason = table.Column<int>(type: "integer", nullable: false),
                    PremoderationStatus = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftVisibility = table.Column<int>(type: "integer", nullable: false),
                    CommentsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PublicationCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    PopularityScore = table.Column<int>(type: "integer", nullable: false),
                    PopularityScoreUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blogs", x => x.BlogId);
                });

            migrationBuilder.CreateTable(
                name: "BoardModerators",
                columns: table => new
                {
                    BoardModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardModerators", x => x.BoardModeratorId);
                });

            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Alias = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ViewPolicy = table.Column<int>(type: "integer", nullable: false),
                    CreateTopicPolicy = table.Column<int>(type: "integer", nullable: false),
                    TopicsCount = table.Column<int>(type: "integer", nullable: false),
                    LastTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastTopicNumber = table.Column<int>(type: "integer", nullable: true),
                    LastTopicTitle = table.Column<string>(type: "text", nullable: true),
                    LastTopicAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastTopicCreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.BoardId);
                });

            migrationBuilder.CreateTable(
                name: "CharacterAttributes",
                columns: table => new
                {
                    CharacterAttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAttributes", x => x.CharacterAttributeId);
                });

            migrationBuilder.CreateTable(
                name: "CharacterEdits",
                columns: table => new
                {
                    CharacterEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterEdits", x => x.CharacterEditId);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDead = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayerLeft = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlayerExiled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsNpc = table.Column<bool>(type: "boolean", nullable: false),
                    AccessPolicy = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.CharacterId);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMessageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "CommentEdits",
                columns: table => new
                {
                    CommentEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentEdits", x => x.CommentEditId);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.CommentId);
                });

            migrationBuilder.CreateTable(
                name: "FundraisingGoals",
                columns: table => new
                {
                    FundraisingGoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundraisingGoals", x => x.FundraisingGoalId);
                });

            migrationBuilder.CreateTable(
                name: "GameAssistants",
                columns: table => new
                {
                    GameAssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameAssistants", x => x.GameAssistantId);
                });

            migrationBuilder.CreateTable(
                name: "GameBlacklists",
                columns: table => new
                {
                    GameBlacklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameBlacklists", x => x.GameBlacklistId);
                });

            migrationBuilder.CreateTable(
                name: "GameReviews",
                columns: table => new
                {
                    GameReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameReviews", x => x.GameReviewId);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PremoderationStatus = table.Column<int>(type: "integer", nullable: false),
                    ClosedReason = table.Column<int>(type: "integer", nullable: false),
                    DraftVisibility = table.Column<int>(type: "integer", nullable: false),
                    IsRecruitmentOpen = table.Column<bool>(type: "boolean", nullable: false),
                    RecruitmentPcLimit = table.Column<int>(type: "integer", nullable: true),
                    RecruitmentStartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecruitmentCount = table.Column<int>(type: "integer", nullable: false),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastPostCreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InactivityWarningUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureWarningUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PopularityScore = table.Column<int>(type: "integer", nullable: false),
                    PopularityScoreUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MasterId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeSchemaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SystemName = table.Column<string>(type: "text", nullable: true),
                    NarrativeSetting = table.Column<string>(type: "text", nullable: true),
                    Info = table.Column<string>(type: "text", nullable: true),
                    HideDiceResult = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPrivateMessages = table.Column<bool>(type: "boolean", nullable: false),
                    HidePostStats = table.Column<bool>(type: "boolean", nullable: false),
                    CommentsAccessMode = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.GameId);
                });

            migrationBuilder.CreateTable(
                name: "GameTags",
                columns: table => new
                {
                    GameTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTags", x => x.GameTagId);
                    table.ForeignKey(
                        name: "FK_GameTags_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlobalChatEventParticipants",
                columns: table => new
                {
                    GlobalChatEventParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOrganizer = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalChatEventParticipants", x => x.GlobalChatEventParticipantId);
                });

            migrationBuilder.CreateTable(
                name: "GlobalChatEvents",
                columns: table => new
                {
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    StartsUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalChatEvents", x => x.GlobalChatEventId);
                });

            migrationBuilder.CreateTable(
                name: "Likes",
                columns: table => new
                {
                    LikeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Likes", x => x.LikeId);
                });

            migrationBuilder.CreateTable(
                name: "MessageEdits",
                columns: table => new
                {
                    MessageEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEdits", x => x.MessageEditId);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian', coalesce(\"Text\", ''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Messages_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_GlobalChatEvents_GlobalChatEventId",
                        column: x => x.GlobalChatEventId,
                        principalTable: "GlobalChatEvents",
                        principalColumn: "GlobalChatEventId");
                });

            migrationBuilder.CreateTable(
                name: "ModeratedProfileNotes",
                columns: table => new
                {
                    ModeratedProfileNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeratedProfileNotes", x => x.ModeratedProfileNoteId);
                });

            migrationBuilder.CreateTable(
                name: "NotepadCategories",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotepadType = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotepadCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "NotepadEntries",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotepadType = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotepadEntries", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_NotepadEntries_NotepadCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NotepadCategories",
                        principalColumn: "CategoryId");
                });

            migrationBuilder.CreateTable(
                name: "PostEdits",
                columns: table => new
                {
                    PostEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostEdits", x => x.PostEditId);
                });

            migrationBuilder.CreateTable(
                name: "PostPendencies",
                columns: table => new
                {
                    PendencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaitingForUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FulfilledUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReminderUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostPendencies", x => x.PendencyId);
                    table.ForeignKey(
                        name: "FK_PostPendencies_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostReviews",
                columns: table => new
                {
                    PostReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostAuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: true),
                    SignValue = table.Column<short>(type: "smallint", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReviews", x => x.PostReviewId);
                    table.ForeignKey(
                        name: "FK_PostReviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GameText = table.Column<string>(type: "text", nullable: false),
                    MetagameText = table.Column<string>(type: "text", nullable: true),
                    SharePrivateWithAll = table.Column<bool>(type: "boolean", nullable: false),
                    PrivateAddresseeSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('russian', regexp_replace(coalesce(\"GameText\", ''), '\\[private(=[^\\]]*)?\\][\\s\\S]*?\\[/private\\]', ' ', 'gi'))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.PostId);
                    table.ForeignKey(
                        name: "FK_Posts_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationNumber = table.Column<int>(type: "integer", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Preview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommentsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publications", x => x.PublicationId);
                    table.ForeignKey(
                        name: "FK_Publications_Blogs_BlogId",
                        column: x => x.BlogId,
                        principalTable: "Blogs",
                        principalColumn: "BlogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomAccesses",
                columns: table => new
                {
                    AccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReaderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Policy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAccesses", x => x.AccessId);
                    table.CheckConstraint("CK_RoomAccesses_TypedTarget", "(\"CharacterId\" IS NOT NULL AND \"ReaderUserId\" IS NULL) OR (\"CharacterId\" IS NULL AND \"ReaderUserId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RoomAccesses_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AccessType = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OrderNumber = table.Column<double>(type: "double precision", nullable: false),
                    ViewPrivateText = table.Column<bool>(type: "boolean", nullable: false),
                    ViewDiceResults = table.Column<bool>(type: "boolean", nullable: false),
                    DiceEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_Rooms_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rooms_Rooms_NextRoomId",
                        column: x => x.NextRoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId");
                    table.ForeignKey(
                        name: "FK_Rooms_Rooms_PreviousRoomId",
                        column: x => x.PreviousRoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId");
                });

            migrationBuilder.CreateTable(
                name: "RubricAccesses",
                columns: table => new
                {
                    RubricAccessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RubricAccesses", x => x.RubricAccessId);
                });

            migrationBuilder.CreateTable(
                name: "Rubrics",
                columns: table => new
                {
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AccessType = table.Column<int>(type: "integer", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rubrics", x => x.RubricId);
                    table.ForeignKey(
                        name: "FK_Rubrics_Blogs_BlogId",
                        column: x => x.BlogId,
                        principalTable: "Blogs",
                        principalColumn: "BlogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Settings = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "TicketResponses",
                columns: table => new
                {
                    TicketResponseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsFromModerator = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketResponses", x => x.TicketResponseId);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestEmail = table.Column<string>(type: "text", nullable: true),
                    TrackingToken = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtype = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    WarningId = table.Column<Guid>(type: "uuid", nullable: true),
                    BanId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_Tickets_Bans_BanId",
                        column: x => x.BanId,
                        principalTable: "Bans",
                        principalColumn: "BanId");
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.TokenId);
                    // FK_Tokens_Blogs_EntityId and FK_Tokens_Games_EntityId are deliberately
                    // absent. Token.EntityId is polymorphic — a GameId, a BlogId or nothing,
                    // depending on Type — and two constraints on one column would demand the
                    // value exist in Blogs AND Games at once, so every invitation INSERT
                    // failed. EF generates both from the Game.Tokens and Blog.Tokens
                    // navigations, which real queries use, so they cannot simply be dropped
                    // from the model. Referential integrity is application logic here.
                    //
                    // This is the one thing regenerating this migration reintroduces, and it
                    // is guarded: InvitationTokenPersistenceShould fails loudly if either
                    // constraint comes back. See DATA_STORAGE.md.
                });

            migrationBuilder.CreateTable(
                name: "TopicEdits",
                columns: table => new
                {
                    TopicEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicEdits", x => x.TopicEditId);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicNumber = table.Column<int>(type: "integer", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsAttached = table.Column<bool>(type: "boolean", nullable: false),
                    AttachOrder = table.Column<int>(type: "integer", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.TopicId);
                    table.ForeignKey(
                        name: "FK_Topics_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Topics_Comments_LastCommentId",
                        column: x => x.LastCommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId");
                });

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Original = table.Column<bool>(type: "boolean", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.UploadId);
                    table.CheckConstraint("CK_Uploads_TypedTarget", "(\"Type\" = 1 AND \"TargetUserId\" IS NOT NULL AND \"TargetCharacterId\" IS NULL AND \"TargetPostId\" IS NULL) OR (\"Type\" = 2 AND \"TargetCharacterId\" IS NOT NULL AND \"TargetUserId\" IS NULL AND \"TargetPostId\" IS NULL) OR (\"Type\" = 3 AND \"TargetPostId\" IS NOT NULL AND \"TargetUserId\" IS NULL AND \"TargetCharacterId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Uploads_Characters_TargetCharacterId",
                        column: x => x.TargetCharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Uploads_Posts_TargetPostId",
                        column: x => x.TargetPostId,
                        principalTable: "Posts",
                        principalColumn: "PostId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AccessPolicy = table.Column<int>(type: "integer", nullable: false),
                    Salt = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PasswordHashVersion = table.Column<int>(type: "integer", nullable: false),
                    RatingDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    QualityRating = table.Column<int>(type: "integer", nullable: false),
                    QuantityRating = table.Column<int>(type: "integer", nullable: false),
                    IsNewbie = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "\"QuantityRating\" < 100", stored: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    BirthdayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ShowBirthday = table.Column<bool>(type: "boolean", nullable: false),
                    Info = table.Column<string>(type: "text", nullable: true),
                    AvatarUploadId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscordId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TelegramId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Uploads_AvatarUploadId",
                        column: x => x.AvatarUploadId,
                        principalTable: "Uploads",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    UserAchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EarnedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.UserAchievementId);
                    table.ForeignKey(
                        name: "FK_UserAchievements_AchievementTypes_AchievementTypeId",
                        column: x => x.AchievementTypeId,
                        principalTable: "AchievementTypes",
                        principalColumn: "AchievementTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAchievements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserAwards",
                columns: table => new
                {
                    UserAwardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwardTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestSeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AwardedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AwardedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAwards", x => x.UserAwardId);
                    table.ForeignKey(
                        name: "FK_UserAwards_AwardTypes_AwardTypeId",
                        column: x => x.AwardTypeId,
                        principalTable: "AwardTypes",
                        principalColumn: "AwardTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAwards_ContestSeries_ContestSeriesId",
                        column: x => x.ContestSeriesId,
                        principalTable: "ContestSeries",
                        principalColumn: "ContestSeriesId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserAwards_Users_AwardedByUserId",
                        column: x => x.AwardedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAwards_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserAwards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBlacklists",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlacklists", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_UserBlacklists_Users_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBlacklists_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserChatLinks",
                columns: table => new
                {
                    UserChatLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChatLinks", x => x.UserChatLinkId);
                    table.ForeignKey(
                        name: "FK_UserChatLinks_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChatLinks_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UserChatLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserContacts",
                columns: table => new
                {
                    UserContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContacts", x => x.UserContactId);
                    table.ForeignKey(
                        name: "FK_UserContacts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserEndorsements",
                columns: table => new
                {
                    UserEndorsementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEndorsements", x => x.UserEndorsementId);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserEndorsements_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginRecords",
                columns: table => new
                {
                    UserLoginRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoginUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginRecords", x => x.UserLoginRecordId);
                    table.ForeignKey(
                        name: "FK_UserLoginRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsernameChangeRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovalToken = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalTokenExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolverComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsernameChangeRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_UsernameChangeRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UsernameChangeRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsernameHistories",
                columns: table => new
                {
                    UsernameHistoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsernameHistories", x => x.UsernameHistoryId);
                    table.ForeignKey(
                        name: "FK_UsernameHistories_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_UsernameHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfileNotes",
                columns: table => new
                {
                    UserProfileNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfileNotes", x => x.UserProfileNoteId);
                    table.ForeignKey(
                        name: "FK_UserProfileNotes_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProfileNotes_Users_SubjectUserId",
                        column: x => x.SubjectUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Warnings",
                columns: table => new
                {
                    WarningId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.WarningId);
                    table.ForeignKey(
                        name: "FK_Warnings_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Warnings_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebsiteTestimonials",
                columns: table => new
                {
                    WebsiteTestimonialId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebsiteTestimonials", x => x.WebsiteTestimonialId);
                    table.ForeignKey(
                        name: "FK_WebsiteTestimonials_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebsiteTestimonials_Users_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WebsiteTestimonials_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "AchievementCategories",
                columns: new[] { "AchievementCategoryId", "Code", "Description", "IconName", "IsActive", "Metric", "SortOrder", "Title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0003-000000000001"), "days_since_registration", "Время с момента регистрации на сайте.", "hourglass", true, 2, 1, "Выслуга лет" },
                    { new Guid("00000000-0000-0000-0003-000000000002"), "game_posts_authored", "Игровые посты в активных играх. Считаются все, включая удаленные игры.", "scroll-quill", true, 1, 2, "Игровые посты" },
                    { new Guid("00000000-0000-0000-0003-000000000003"), "post_review_score_sum", "Сумма положительных оценок твоих игровых постов. Отрицательные оценки рейтинг не уменьшают.", "laurels", true, 3, 3, "Рейтинг" },
                    { new Guid("00000000-0000-0000-0003-000000000004"), "games_hosted", "Игры, где ты мастер или ассистент.", "scepter", true, 4, 4, "Игры в роли ведущего" },
                    { new Guid("00000000-0000-0000-0003-000000000005"), "games_played", "Игры, где у тебя есть активный или бывший персонаж.", "sword", true, 5, 5, "Игры в роли игрока" },
                    { new Guid("00000000-0000-0000-0003-000000000006"), "blogs_hosted", "Блоги, где ты автор или ассистент.", "book", true, 6, 6, "Блоги в роли ведущего" },
                    { new Guid("00000000-0000-0000-0003-000000000007"), "publications_authored", "Статьи в блогах. Черновики тоже считаются.", "papers", true, 12, 7, "Публикации" },
                    { new Guid("00000000-0000-0000-0003-000000000008"), "topics_authored", "Форумные топики, которые ты создал.", "stabbed-note", true, 7, 8, "Топики" },
                    { new Guid("00000000-0000-0000-0003-000000000009"), "comments_authored", "Все комментарии: форум, блоги, игры, публикации.", "discussion", true, 8, 9, "Комментарии" },
                    { new Guid("00000000-0000-0000-0003-00000000000a"), "global_chat_messages", "Сообщения в глобальном чате сайта.", "talk", true, 9, 10, "Глобальный чат" },
                    { new Guid("00000000-0000-0000-0003-00000000000b"), "likes_received", "Лайки на топиках, публикациях, комментариях и сообщениях чата. Игровые посты учитываются через \"Рейтинг\".", "heart-organ", true, 13, 11, "Лайки" },
                    { new Guid("00000000-0000-0000-0003-00000000000c"), "game_drops", "Игры, которые ты покинул добровольно. Смерть персонажа и изгнание мастером не считаются.", "walking-boot", true, 11, 12, "Дропы" },
                    { new Guid("00000000-0000-0000-0003-00000000000d"), "bans_received", "Баны, полученные от модерации.", "plastic-duck", true, 10, 13, "Баны" }
                });

            migrationBuilder.InsertData(
                table: "AwardTypes",
                columns: new[] { "AwardTypeId", "Code", "Description", "IconName", "IsActive", "SortOrder", "Tier", "Title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "contest_first", "Победитель конкурса", "trophy-cup", true, 1, 1, "Литконкурс" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "contest_second", "Серебряный призер конкурса", "trophy-cup", true, 2, 2, "Литконкурс" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "contest_third", "Бронзовый призер конкурса", "trophy-cup", true, 3, 3, "Литконкурс" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "popular_vote", "Лучшая работа конкурса по голосованию участников", "ribbon-medal", true, 4, 1, "Народное признание, например" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "best_critic", "Лучшие рецензии сезона по решению жюри", "quill-ink", true, 5, 1, "Лучший критик" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "guesser", "Угадал больше всех авторов конкурсных работ", "magnifying-glass", true, 6, 1, "Угадайка" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "honorary_goblin", "Бывший гоблин, отдавший сообществу годы службы", "goblin", true, 7, 5, "Почетный гоблин" }
                });

            migrationBuilder.InsertData(
                table: "Boards",
                columns: new[] { "BoardId", "Alias", "CreateTopicPolicy", "Description", "LastTopicAuthorId", "LastTopicCreatedUtc", "LastTopicId", "LastTopicNumber", "LastTopicTitle", "Order", "Title", "TopicsCount", "ViewPolicy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "general", 32, "Жизнь сообщества и решения администрации", null, null, null, null, null, 1, "Общий", 1, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "game-systems", 32, "Обсуждение правил и помощь в выборе системы", null, null, null, null, null, 2, "Игровые системы", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "looking-for-group", 32, "Набор игроков в игру или поиск мастера", null, null, null, null, null, 3, "Поиск мастера и игроков", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "ideas", 32, "Обкатка задумок и поиск единомышленников", null, null, null, null, null, 4, "Котел идей", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "contests", 4, "Литературные и творческие состязания", null, null, null, null, null, 5, "Конкурсы", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "off-topic", 32, "Музыка, книги, кино, мемы и все остальное", null, null, null, null, null, 6, "Под столом", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "forum-games", 32, "Словесные игры, ассоциации и прочие развлечения", null, null, null, null, null, 7, "Неролевые игры", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "improvements", 32, "Идеи и предложения по развитию сайта", null, null, null, null, null, 8, "Улучшение сайта", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "bugs", 32, "Сообщения об ошибках на сайте", null, null, null, null, null, 9, "Ошибки", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-00000000000a"), "newbies", 32, "Руководства, ответы на вопросы и помощь новичкам", null, null, null, null, null, 10, "Для новичков", 0, 64 },
                    { new Guid("00000000-0000-0000-0000-00000000000b"), "news", 4, "Официальные новости, обновления и статистика", null, null, null, null, null, 11, "Новости проекта", 0, 64 }
                });

            migrationBuilder.InsertData(
                table: "Chats",
                columns: new[] { "ChatId", "LastMessageId", "PublicId", "RoomId", "Title", "Type" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, "global", null, "Глобальный чат", 2 });

            migrationBuilder.InsertData(
                table: "ContestSeries",
                columns: new[] { "ContestSeriesId", "ContestType", "IsActive", "Number", "TopicUrl", "Year" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0004-000000000001"), 0, true, 23, "https://dm.am/forum/topic/contest-results-lit-23", 2024 },
                    { new Guid("00000000-0000-0000-0004-000000000002"), 0, true, 22, "https://dm.am/forum/topic/contest-results-lit-22", 2023 },
                    { new Guid("00000000-0000-0000-0004-000000000003"), 0, true, 21, "https://dm.am/forum/topic/contest-results-lit-21", 2023 },
                    { new Guid("00000000-0000-0000-0004-000000000004"), 0, true, 20, "https://dm.am/forum/topic/contest-results-lit-20", 2022 },
                    { new Guid("00000000-0000-0000-0004-000000000005"), 1, true, 2, "https://dm.am/forum/topic/contest-results-art-2", 2024 },
                    { new Guid("00000000-0000-0000-0004-000000000006"), 1, true, 1, "https://dm.am/forum/topic/contest-results-art-1", 2023 }
                });

            migrationBuilder.InsertData(
                table: "FundraisingGoals",
                columns: new[] { "FundraisingGoalId", "CollectedAmount", "GoalAmount", "ModifiedUtc", "UpdatedByUserId" },
                values: new object[] { new Guid("00000000-0000-0000-0005-000000000001"), 17000m, 50000m, new DateTimeOffset(new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.InsertData(
                table: "TagGroups",
                columns: new[] { "TagGroupId", "Description", "SortOrder", "Title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0005-000000000001"), "Ролевая система или набор правил, по которым ведется игра", 0, "Система" },
                    { new Guid("00000000-0000-0000-0005-000000000002"), "Жанр и сеттинг игрового мира", 1, "Жанр" },
                    { new Guid("00000000-0000-0000-0005-000000000003"), "Тип игрового процесса и взаимодействия между участниками", 2, "Формат игры" },
                    { new Guid("00000000-0000-0000-0005-000000000004"), "Стиль и объем игровых постов", 3, "Формат постов" },
                    { new Guid("00000000-0000-0000-0005-000000000005"), "Ожидаемая скорость игры и частота постов", 4, "Темп" },
                    { new Guid("00000000-0000-0000-0005-000000000006"), "Особые требования и ограничения для участников", 5, "Ограничения" },
                    { new Guid("00000000-0000-0000-0005-000000000007"), "Игры от новичков и для новичков", 6, "Новички" },
                    { new Guid("00000000-0000-0000-0005-000000000008"), "Контент, требующий осознанного согласия участников", 7, "Деликатный контент" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "AccessPolicy", "AvatarUploadId", "BirthdayDate", "CreatedUtc", "DiscordId", "Email", "Gender", "Info", "IsRemoved", "LastActivityUtc", "Location", "Name", "PasswordHash", "PasswordHashVersion", "QualityRating", "QuantityRating", "RatingDisabled", "Role", "Salt", "ShowBirthday", "Status", "TelegramId", "Username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), 0, null, null, new DateTimeOffset(new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "system@dm.local", 0, null, false, null, null, null, "", 0, 0, 0, true, 6, "", false, null, null, "Робот-Администратор" });

            migrationBuilder.InsertData(
                table: "AchievementTypes",
                columns: new[] { "AchievementTypeId", "AchievementCategoryId", "Code", "Threshold", "Tier", "Title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), new Guid("00000000-0000-0000-0003-000000000002"), "POSTS_100", 100, 1, "Простые начала" },
                    { new Guid("00000000-0000-0000-0002-000000000002"), new Guid("00000000-0000-0000-0003-000000000002"), "POSTS_500", 500, 2, "Продолжение следует" },
                    { new Guid("00000000-0000-0000-0002-000000000003"), new Guid("00000000-0000-0000-0003-000000000002"), "POSTS_2000", 2000, 3, "Долгая партия" },
                    { new Guid("00000000-0000-0000-0002-000000000004"), new Guid("00000000-0000-0000-0003-000000000002"), "POSTS_5000", 5000, 4, "Приключение в жизнь" },
                    { new Guid("00000000-0000-0000-0002-000000000005"), new Guid("00000000-0000-0000-0003-000000000001"), "DAYS_366", 366, 1, "Поселенец" },
                    { new Guid("00000000-0000-0000-0002-000000000006"), new Guid("00000000-0000-0000-0003-000000000001"), "DAYS_1827", 1827, 2, "Старожил" },
                    { new Guid("00000000-0000-0000-0002-000000000007"), new Guid("00000000-0000-0000-0003-000000000001"), "DAYS_3653", 3653, 3, "Ветеран" },
                    { new Guid("00000000-0000-0000-0002-000000000008"), new Guid("00000000-0000-0000-0003-000000000001"), "DAYS_5479", 5479, 4, "Древний" },
                    { new Guid("00000000-0000-0000-0002-000000000009"), new Guid("00000000-0000-0000-0003-000000000003"), "RATING_100", 100, 1, "Подающий надежды" },
                    { new Guid("00000000-0000-0000-0002-00000000000a"), new Guid("00000000-0000-0000-0003-000000000003"), "RATING_250", 250, 2, "Видный талант" },
                    { new Guid("00000000-0000-0000-0002-00000000000b"), new Guid("00000000-0000-0000-0003-000000000003"), "RATING_500", 500, 3, "Опытный зубр" },
                    { new Guid("00000000-0000-0000-0002-00000000000c"), new Guid("00000000-0000-0000-0003-000000000003"), "RATING_1000", 1000, 4, "Мастодонт-аксакал" },
                    { new Guid("00000000-0000-0000-0002-00000000000d"), new Guid("00000000-0000-0000-0003-000000000004"), "HOST_3", 3, 1, "Подмастерье" },
                    { new Guid("00000000-0000-0000-0002-00000000000e"), new Guid("00000000-0000-0000-0003-000000000004"), "HOST_10", 10, 2, "Мастер" },
                    { new Guid("00000000-0000-0000-0002-00000000000f"), new Guid("00000000-0000-0000-0003-000000000004"), "HOST_30", 30, 3, "Грандмастер" },
                    { new Guid("00000000-0000-0000-0002-000000000010"), new Guid("00000000-0000-0000-0003-000000000004"), "HOST_100", 100, 4, "Архитектор миров" },
                    { new Guid("00000000-0000-0000-0002-000000000011"), new Guid("00000000-0000-0000-0003-000000000005"), "PLAY_5", 5, 1, "Искатель" },
                    { new Guid("00000000-0000-0000-0002-000000000012"), new Guid("00000000-0000-0000-0003-000000000005"), "PLAY_20", 20, 2, "Авантюрист" },
                    { new Guid("00000000-0000-0000-0002-000000000013"), new Guid("00000000-0000-0000-0003-000000000005"), "PLAY_100", 100, 3, "Герой" },
                    { new Guid("00000000-0000-0000-0002-000000000014"), new Guid("00000000-0000-0000-0003-000000000005"), "PLAY_500", 500, 4, "Легенда" },
                    { new Guid("00000000-0000-0000-0002-000000000015"), new Guid("00000000-0000-0000-0003-000000000006"), "BLOGS_1", 1, 1, "Свежий взгляд" },
                    { new Guid("00000000-0000-0000-0002-000000000016"), new Guid("00000000-0000-0000-0003-000000000006"), "BLOGS_5", 5, 2, "Небольшая подборка" },
                    { new Guid("00000000-0000-0000-0002-000000000017"), new Guid("00000000-0000-0000-0003-000000000006"), "BLOGS_15", 15, 3, "Именная коллекция" },
                    { new Guid("00000000-0000-0000-0002-000000000018"), new Guid("00000000-0000-0000-0003-000000000006"), "BLOGS_50", 50, 4, "Библиотека" },
                    { new Guid("00000000-0000-0000-0002-000000000019"), new Guid("00000000-0000-0000-0003-000000000008"), "TOPICS_5", 5, 1, "Повод для обсуждения" },
                    { new Guid("00000000-0000-0000-0002-00000000001a"), new Guid("00000000-0000-0000-0003-000000000008"), "TOPICS_25", 25, 2, "Занятные темы" },
                    { new Guid("00000000-0000-0000-0002-00000000001b"), new Guid("00000000-0000-0000-0003-000000000008"), "TOPICS_100", 100, 3, "Дневная повестка" },
                    { new Guid("00000000-0000-0000-0002-00000000001c"), new Guid("00000000-0000-0000-0003-000000000008"), "TOPICS_500", 500, 4, "На целый раздел" },
                    { new Guid("00000000-0000-0000-0002-00000000001d"), new Guid("00000000-0000-0000-0003-000000000009"), "COMMENTS_100", 100, 1, "Свои пять копеек" },
                    { new Guid("00000000-0000-0000-0002-00000000001e"), new Guid("00000000-0000-0000-0003-000000000009"), "COMMENTS_500", 500, 2, "Живое участие" },
                    { new Guid("00000000-0000-0000-0002-00000000001f"), new Guid("00000000-0000-0000-0003-000000000009"), "COMMENTS_2000", 2000, 3, "В гуще событий" },
                    { new Guid("00000000-0000-0000-0002-000000000020"), new Guid("00000000-0000-0000-0003-000000000009"), "COMMENTS_10000", 10000, 4, "Всегда есть что сказать" },
                    { new Guid("00000000-0000-0000-0002-000000000021"), new Guid("00000000-0000-0000-0003-00000000000a"), "CHAT_500", 500, 1, "Прохожий" },
                    { new Guid("00000000-0000-0000-0002-000000000022"), new Guid("00000000-0000-0000-0003-00000000000a"), "CHAT_5000", 5000, 2, "Свой человек" },
                    { new Guid("00000000-0000-0000-0002-000000000023"), new Guid("00000000-0000-0000-0003-00000000000a"), "CHAT_25000", 25000, 3, "Чат-завсегдатай" },
                    { new Guid("00000000-0000-0000-0002-000000000024"), new Guid("00000000-0000-0000-0003-00000000000a"), "CHAT_100000", 100000, 4, "Вечный онлайн" },
                    { new Guid("00000000-0000-0000-0002-000000000025"), new Guid("00000000-0000-0000-0003-00000000000d"), "BANS_1", 1, 1, "Яйцо с характером" },
                    { new Guid("00000000-0000-0000-0002-000000000026"), new Guid("00000000-0000-0000-0003-00000000000d"), "BANS_3", 3, 2, "Выпавший из гнезда" },
                    { new Guid("00000000-0000-0000-0002-000000000027"), new Guid("00000000-0000-0000-0003-00000000000d"), "BANS_10", 10, 3, "Утенок-террорист" },
                    { new Guid("00000000-0000-0000-0002-000000000028"), new Guid("00000000-0000-0000-0003-00000000000d"), "BANS_30", 30, 4, "Селезень-Рецидивист" },
                    { new Guid("00000000-0000-0000-0002-000000000029"), new Guid("00000000-0000-0000-0003-00000000000c"), "DROPS_1", 1, 1, "Перекати-поле" },
                    { new Guid("00000000-0000-0000-0002-00000000002a"), new Guid("00000000-0000-0000-0003-00000000000c"), "DROPS_3", 3, 2, "Беглец" },
                    { new Guid("00000000-0000-0000-0002-00000000002b"), new Guid("00000000-0000-0000-0003-00000000000c"), "DROPS_10", 10, 3, "Дезертир" },
                    { new Guid("00000000-0000-0000-0002-00000000002c"), new Guid("00000000-0000-0000-0003-00000000000c"), "DROPS_30", 30, 4, "Пропавший без вести" },
                    { new Guid("00000000-0000-0000-0002-00000000002d"), new Guid("00000000-0000-0000-0003-000000000007"), "PUBS_5", 5, 1, "Проба пера" },
                    { new Guid("00000000-0000-0000-0002-00000000002e"), new Guid("00000000-0000-0000-0003-000000000007"), "PUBS_25", 25, 2, "Мысли вслух" },
                    { new Guid("00000000-0000-0000-0002-00000000002f"), new Guid("00000000-0000-0000-0003-000000000007"), "PUBS_100", 100, 3, "Постоянная рубрика" },
                    { new Guid("00000000-0000-0000-0002-000000000030"), new Guid("00000000-0000-0000-0003-000000000007"), "PUBS_500", 500, 4, "Без строчки ни дня" },
                    { new Guid("00000000-0000-0000-0002-000000000031"), new Guid("00000000-0000-0000-0003-00000000000b"), "LIKES_25", 25, 1, "В узких кругах" },
                    { new Guid("00000000-0000-0000-0002-000000000032"), new Guid("00000000-0000-0000-0003-00000000000b"), "LIKES_100", 100, 2, "Душа компании" },
                    { new Guid("00000000-0000-0000-0002-000000000033"), new Guid("00000000-0000-0000-0003-00000000000b"), "LIKES_500", 500, 3, "Народный любимец" },
                    { new Guid("00000000-0000-0000-0002-000000000034"), new Guid("00000000-0000-0000-0003-00000000000b"), "LIKES_2000", 2000, 4, "Первый в сердцах" }
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "TagId", "Description", "ShortId", "SortOrder", "TagGroupId", "Title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Простая система с кубиком d6", 1, 0, new Guid("00000000-0000-0000-0005-000000000001"), "Black Bird Pie" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Dungeons & Dragons — все редакции классической ролевой системы", 2, 1, new Guid("00000000-0000-0000-0005-000000000001"), "D&D" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Dungeons & Dragons 5th Edition", 3, 2, new Guid("00000000-0000-0000-0005-000000000001"), "D&D 5e" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Системы на основе процентного броска", 4, 3, new Guid("00000000-0000-0000-0005-000000000001"), "D100" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "Система для совместного создания мира", 5, 4, new Guid("00000000-0000-0000-0005-000000000001"), "Dawn of Worlds" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "Адаптация сеттинга Fallout", 6, 5, new Guid("00000000-0000-0000-0005-000000000001"), "Fallout" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "Без комментариев", 7, 6, new Guid("00000000-0000-0000-0005-000000000001"), "FATAL" },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "Нарративная система с аспектами и фейт-пойнтами", 8, 7, new Guid("00000000-0000-0000-0005-000000000001"), "Fate" },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "Универсальный движок для реализации практически любого концепта", 9, 8, new Guid("00000000-0000-0000-0005-000000000001"), "FUDGE" },
                    { new Guid("00000000-0000-0000-0000-00000000000a"), "Универсальная система на базе броска 3d6 vs Сложность", 10, 9, new Guid("00000000-0000-0000-0005-000000000001"), "GURPS" },
                    { new Guid("00000000-0000-0000-0000-00000000000b"), "Система от R. Talsorian Games (Cyberpunk 2020 и другие)", 11, 10, new Guid("00000000-0000-0000-0005-000000000001"), "Interlock" },
                    { new Guid("00000000-0000-0000-0000-00000000000c"), "Система для создания эпических историй", 12, 11, new Guid("00000000-0000-0000-0005-000000000001"), "Microscope" },
                    { new Guid("00000000-0000-0000-0000-00000000000d"), "Pathfinder первой редакции", 13, 12, new Guid("00000000-0000-0000-0005-000000000001"), "Pathfinder 1e" },
                    { new Guid("00000000-0000-0000-0000-00000000000e"), "Pathfinder второй редакции", 14, 13, new Guid("00000000-0000-0000-0005-000000000001"), "Pathfinder 2e" },
                    { new Guid("00000000-0000-0000-0000-00000000000f"), "Нарративные системы на базе 2d6 vs Сложность", 15, 14, new Guid("00000000-0000-0000-0005-000000000001"), "PbtA" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), "Минималистичная комедийная система", 16, 15, new Guid("00000000-0000-0000-0005-000000000001"), "Risus" },
                    { new Guid("00000000-0000-0000-0000-000000000011"), "Легковесная универсальная система — Fast! Furious! Fun!", 17, 16, new Guid("00000000-0000-0000-0005-000000000001"), "Savage Worlds" },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "Sci-fi спин-офф Pathfinder", 18, 17, new Guid("00000000-0000-0000-0005-000000000001"), "Starfinder 1e" },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "Starfinder второй редакции", 19, 18, new Guid("00000000-0000-0000-0005-000000000001"), "Starfinder 2e" },
                    { new Guid("00000000-0000-0000-0000-000000000014"), "Системы по вселенной Warhammer", 20, 19, new Guid("00000000-0000-0000-0005-000000000001"), "Warhammer" },
                    { new Guid("00000000-0000-0000-0000-000000000015"), "Мир Тьмы — вампиры, оборотни, маги", 21, 20, new Guid("00000000-0000-0000-0005-000000000001"), "World of Darkness" },
                    { new Guid("00000000-0000-0000-0000-000000000016"), "Оригинальная система от мастера игры", 22, 21, new Guid("00000000-0000-0000-0005-000000000001"), "Авторская" },
                    { new Guid("00000000-0000-0000-0000-000000000017"), "Психологическая детективная командная игра", 23, 22, new Guid("00000000-0000-0000-0005-000000000001"), "Мафия" },
                    { new Guid("00000000-0000-0000-0000-000000000018"), "Игра без формальной системы правил", 24, 23, new Guid("00000000-0000-0000-0005-000000000001"), "Словеска" },
                    { new Guid("00000000-0000-0000-0000-000000000019"), "Отечественная система ролевых игр", 25, 24, new Guid("00000000-0000-0000-0005-000000000001"), "Эра Водолея" },
                    { new Guid("00000000-0000-0000-0000-00000000001a"), "Переосмысление исторических событий", 26, 0, new Guid("00000000-0000-0000-0005-000000000002"), "Альтернативная история" },
                    { new Guid("00000000-0000-0000-0000-00000000001b"), "Акцент на экшн и сражениях", 27, 1, new Guid("00000000-0000-0000-0005-000000000002"), "Боевик" },
                    { new Guid("00000000-0000-0000-0000-00000000001c"), "Расследования и разгадывание тайн", 28, 2, new Guid("00000000-0000-0000-0005-000000000002"), "Детектив" },
                    { new Guid("00000000-0000-0000-0000-00000000001d"), "Зомби-апокалипсис и выживание", 29, 3, new Guid("00000000-0000-0000-0005-000000000002"), "Зомби" },
                    { new Guid("00000000-0000-0000-0000-00000000001e"), "Действие в реальную историческую эпоху", 30, 4, new Guid("00000000-0000-0000-0005-000000000002"), "Историческое" },
                    { new Guid("00000000-0000-0000-0000-00000000001f"), "Высокие технологии, низкий уровень жизни", 31, 5, new Guid("00000000-0000-0000-0005-000000000002"), "Киберпанк" },
                    { new Guid("00000000-0000-0000-0000-000000000020"), "Юмор и абсурдные ситуации", 32, 6, new Guid("00000000-0000-0000-0005-000000000002"), "Комедия" },
                    { new Guid("00000000-0000-0000-0000-000000000021"), "Эпические приключения в космосе", 33, 7, new Guid("00000000-0000-0000-0005-000000000002"), "Космоопера" },
                    { new Guid("00000000-0000-0000-0000-000000000022"), "Сверхъестественные элементы и тайны", 34, 8, new Guid("00000000-0000-0000-0005-000000000002"), "Мистика" },
                    { new Guid("00000000-0000-0000-0000-000000000023"), "Современный реалистичный сеттинг", 35, 9, new Guid("00000000-0000-0000-0005-000000000002"), "Наши дни" },
                    { new Guid("00000000-0000-0000-0000-000000000024"), "Мир после катастрофы", 36, 10, new Guid("00000000-0000-0000-0005-000000000002"), "Постапокалипсис" },
                    { new Guid("00000000-0000-0000-0000-000000000025"), "Сюрреалистичные и необычные миры", 37, 11, new Guid("00000000-0000-0000-0005-000000000002"), "Психоделика" },
                    { new Guid("00000000-0000-0000-0000-000000000026"), "Паровые технологии и викторианская эстетика", 38, 12, new Guid("00000000-0000-0000-0005-000000000002"), "Стимпанк" },
                    { new Guid("00000000-0000-0000-0000-000000000027"), "Напряжение и саспенс", 39, 13, new Guid("00000000-0000-0000-0005-000000000002"), "Триллер" },
                    { new Guid("00000000-0000-0000-0000-000000000028"), "Нарочито нелепый и провокационный контент", 40, 14, new Guid("00000000-0000-0000-0005-000000000002"), "Трэш" },
                    { new Guid("00000000-0000-0000-0000-000000000029"), "Хоррор и атмосфера страха", 41, 15, new Guid("00000000-0000-0000-0005-000000000002"), "Ужасы" },
                    { new Guid("00000000-0000-0000-0000-00000000002a"), "Научная фантастика и будущее", 42, 16, new Guid("00000000-0000-0000-0005-000000000002"), "Фантастика" },
                    { new Guid("00000000-0000-0000-0000-00000000002b"), "Магия, мечи и волшебные миры", 43, 17, new Guid("00000000-0000-0000-0005-000000000002"), "Фэнтези" },
                    { new Guid("00000000-0000-0000-0000-00000000002c"), "Исследование подземелий и сражения с монстрами", 44, 0, new Guid("00000000-0000-0000-0005-000000000003"), "Dungeon Crawl" },
                    { new Guid("00000000-0000-0000-0000-00000000002d"), "Противостояние между игроками", 45, 1, new Guid("00000000-0000-0000-0005-000000000003"), "PvP" },
                    { new Guid("00000000-0000-0000-0000-00000000002e"), "Борьба за выживание в суровых условиях", 46, 2, new Guid("00000000-0000-0000-0005-000000000003"), "Выживание" },
                    { new Guid("00000000-0000-0000-0000-00000000002f"), "Открытый мир без сюжетных ограничений", 47, 3, new Guid("00000000-0000-0000-0005-000000000003"), "Песочница" },
                    { new Guid("00000000-0000-0000-0000-000000000030"), "Управление ресурсами и принятие глобальных решений", 48, 4, new Guid("00000000-0000-0000-0005-000000000003"), "Стратегия" },
                    { new Guid("00000000-0000-0000-0000-000000000031"), "Фокус на развитии истории", 49, 5, new Guid("00000000-0000-0000-0005-000000000003"), "Сюжетная" },
                    { new Guid("00000000-0000-0000-0000-000000000032"), "Тактические бои и позиционирование", 50, 6, new Guid("00000000-0000-0000-0005-000000000003"), "Тактика" },
                    { new Guid("00000000-0000-0000-0000-000000000033"), "Короткие посты в 1-3 абзаца", 51, 0, new Guid("00000000-0000-0000-0005-000000000004"), "Короткопост" },
                    { new Guid("00000000-0000-0000-0000-000000000034"), "Развернутые литературные посты", 52, 1, new Guid("00000000-0000-0000-0005-000000000004"), "Литературная" },
                    { new Guid("00000000-0000-0000-0000-000000000035"), "Посты раз в несколько дней", 53, 0, new Guid("00000000-0000-0000-0005-000000000005"), "Неторопливый" },
                    { new Guid("00000000-0000-0000-0000-000000000036"), "Несколько постов в день", 54, 1, new Guid("00000000-0000-0000-0005-000000000005"), "Скоростной" },
                    { new Guid("00000000-0000-0000-0000-000000000037"), "Нецензурная лексика запрещена", 55, 0, new Guid("00000000-0000-0000-0005-000000000006"), "Без мата" },
                    { new Guid("00000000-0000-0000-0000-000000000038"), "Минимум жестокости и крови", 56, 1, new Guid("00000000-0000-0000-0005-000000000006"), "Без насилия" },
                    { new Guid("00000000-0000-0000-0000-000000000039"), "Повышенные требования к грамотности", 57, 2, new Guid("00000000-0000-0000-0005-000000000006"), "Grammar Nazi" },
                    { new Guid("00000000-0000-0000-0000-00000000003a"), "Игра для знакомой компании", 58, 3, new Guid("00000000-0000-0000-0005-000000000006"), "Для своих" },
                    { new Guid("00000000-0000-0000-0000-00000000003b"), "Обсуждение игровых вопросов во внешнем мессенджере", 59, 4, new Guid("00000000-0000-0000-0005-000000000006"), "Обязателен мессенджер" },
                    { new Guid("00000000-0000-0000-0000-00000000003c"), "Игра подходит для начинающих", 60, 0, new Guid("00000000-0000-0000-0005-000000000007"), "Для новичков" },
                    { new Guid("00000000-0000-0000-0000-00000000003d"), "Мастер игры — начинающий", 61, 1, new Guid("00000000-0000-0000-0005-000000000007"), "Мастер-новичок" },
                    { new Guid("00000000-0000-0000-0000-00000000003e"), "Возможны продолжительные периоды без постов", 62, 2, new Guid("00000000-0000-0000-0005-000000000005"), "Сухие сезоны" },
                    { new Guid("00000000-0000-0000-0000-00000000003f"), "Erotic Role-Play: [tipimg:/images/erp-tooltip.gif]эротические сцены[/tipimg] как основа игрового процесса", 63, 0, new Guid("00000000-0000-0000-0005-000000000008"), "ERP" },
                    { new Guid("00000000-0000-0000-0000-000000000040"), "Чернуха, максимально шокирующий и отталкивающий контент без ограничений", 64, 1, new Guid("00000000-0000-0000-0005-000000000008"), "Шок-контент" },
                    { new Guid("00000000-0000-0000-0000-000000000041"), "Игра затрагивает спорные или чувствительные социальные темы", 65, 2, new Guid("00000000-0000-0000-0005-000000000008"), "Острые темы" }
                });

            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "TopicId", "AttachOrder", "AuthorId", "BoardId", "CreatedUtc", "DeletedByUserId", "DeletedUtc", "IsAttached", "IsClosed", "IsRemoved", "LastCommentId", "Text", "Title", "TopicNumber" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), null, new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, false, false, null, "Ваши отзывы отсюда попадают (после минимального анализа на нарушения правил) прямиком на главную.", "Отзывы о ДМ", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000100"), null, new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2020, 1, 1, 0, 0, 1, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, false, false, null, "Здесь можно обсудить решения модераторов и администрации. Конструктивная критика приветствуется.", "Обсуждение действий администрации", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievementCategories_Code",
                table: "AchievementCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AchievementCategories_Metric",
                table: "AchievementCategories",
                column: "Metric",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AchievementTypes_AchievementCategoryId",
                table: "AchievementTypes",
                column: "AchievementCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AchievementTypes_Code",
                table: "AchievementTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwardTypes_Code",
                table: "AwardTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bans_AuthorId",
                table: "Bans",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Bans_TargetUserId",
                table: "Bans",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogAssistants_BlogId",
                table: "BlogAssistants",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogAssistants_UserId",
                table: "BlogAssistants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogBlacklists_BlockedByUserId",
                table: "BlogBlacklists",
                column: "BlockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogBlacklists_BlockedUserId",
                table: "BlogBlacklists",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogBlacklists_BlogId",
                table: "BlogBlacklists",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_AuthorId",
                table: "Blogs",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_DeletedByUserId",
                table: "Blogs",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Blogs_MentorId",
                table: "Blogs",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardModerators_BoardId",
                table: "BoardModerators",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardModerators_UserId",
                table: "BoardModerators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastTopicAuthorId",
                table: "Boards",
                column: "LastTopicAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastTopicId",
                table: "Boards",
                column: "LastTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAttributes_CharacterId",
                table: "CharacterAttributes",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEdits_CharacterId",
                table: "CharacterEdits",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEdits_EditorUserId",
                table: "CharacterEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_AuthorId",
                table: "Characters",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_DeletedByUserId",
                table: "Characters",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_GameId",
                table: "Characters",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_LastMessageId",
                table: "Chats",
                column: "LastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_PublicId",
                table: "Chats",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentEdits_CommentId",
                table: "CommentEdits",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentEdits_EditorUserId",
                table: "CommentEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_DeletedByUserId",
                table: "Comments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_EntityId",
                table: "Comments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestSeries_ContestType_Number",
                table: "ContestSeries",
                columns: new[] { "ContestType", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundraisingGoals_UpdatedByUserId",
                table: "FundraisingGoals",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAssistants_GameId",
                table: "GameAssistants",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAssistants_UserId",
                table: "GameAssistants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBlacklists_BlockedByUserId",
                table: "GameBlacklists",
                column: "BlockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBlacklists_BlockedUserId",
                table: "GameBlacklists",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameBlacklists_GameId",
                table: "GameBlacklists",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_AuthorId_GameId",
                table: "GameReviews",
                columns: new[] { "AuthorId", "GameId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_DeletedByUserId",
                table: "GameReviews",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_GameId",
                table: "GameReviews",
                column: "GameId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_ModifiedByUserId",
                table: "GameReviews",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_DeletedByUserId",
                table: "Games",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_MasterId",
                table: "Games",
                column: "MasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_MentorId",
                table: "Games",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTags_GameId",
                table: "GameTags",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTags_TagId",
                table: "GameTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalChatEventParticipants_GlobalChatEventId",
                table: "GlobalChatEventParticipants",
                column: "GlobalChatEventId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalChatEventParticipants_UserId",
                table: "GlobalChatEventParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalChatEvents_CreatedByUserId",
                table: "GlobalChatEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_DeletedByUserId",
                table: "Likes",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_EntityType_EntityId",
                table: "Likes",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Likes_UserId",
                table: "Likes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEdits_EditorUserId",
                table: "MessageEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEdits_MessageId",
                table: "MessageEdits",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId_CreatedUtc_MessageId",
                table: "Messages",
                columns: new[] { "ChatId", "CreatedUtc", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GlobalChatEventId",
                table: "Messages",
                column: "GlobalChatEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SearchVector",
                table: "Messages",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_UserId",
                table: "Messages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedProfileNotes_AuthorId",
                table: "ModeratedProfileNotes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedProfileNotes_UserId",
                table: "ModeratedProfileNotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadCategories_AuthorId",
                table: "NotepadCategories",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadCategories_ContainerId_NotepadType_OwnerId",
                table: "NotepadCategories",
                columns: new[] { "ContainerId", "NotepadType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotepadCategories_DeletedByUserId",
                table: "NotepadCategories",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_AuthorId",
                table: "NotepadEntries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_CategoryId",
                table: "NotepadEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_ContainerId_NotepadType_OwnerId",
                table: "NotepadEntries",
                columns: new[] { "ContainerId", "NotepadType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_DeletedByUserId",
                table: "NotepadEntries",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_CreatedUtc",
                table: "PendingRegistrations",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_TokenId",
                table: "PendingRegistrations",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodDigestTopics_Year",
                table: "PeriodDigestTopics",
                column: "Year",
                unique: true,
                filter: "\"Month\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodDigestTopics_Year_Month",
                table: "PeriodDigestTopics",
                columns: new[] { "Year", "Month" },
                unique: true,
                filter: "\"Month\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostEdits_EditorUserId",
                table: "PostEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostEdits_PostId",
                table: "PostEdits",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_CharacterId",
                table: "PostPendencies",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_CreatedById",
                table: "PostPendencies",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_RoomId",
                table: "PostPendencies",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_PostPendencies_WaitingForUserId",
                table: "PostPendencies",
                column: "WaitingForUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_AuthorId_PostId",
                table: "PostReviews",
                columns: new[] { "AuthorId", "PostId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_CreatedUtc",
                table: "PostReviews",
                column: "CreatedUtc",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_DeletedByUserId",
                table: "PostReviews",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_GameId",
                table: "PostReviews",
                column: "GameId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_ModifiedByUserId",
                table: "PostReviews",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_PostAuthorId",
                table: "PostReviews",
                column: "PostAuthorId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PostReviews_PostId",
                table: "PostReviews",
                column: "PostId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CharacterId",
                table: "Posts",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedUtc",
                table: "Posts",
                column: "CreatedUtc",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_DeletedByUserId",
                table: "Posts",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_RoomId",
                table: "Posts",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_SearchVector",
                table: "Posts",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_AuthorId",
                table: "Publications",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_BlogId",
                table: "Publications",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_CreatedUtc",
                table: "Publications",
                column: "CreatedUtc",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_DeletedByUserId",
                table: "Publications",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_ModifiedByUserId",
                table: "Publications",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_RubricId",
                table: "Publications",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_CharacterId",
                table: "RoomAccesses",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_ReaderUserId",
                table: "RoomAccesses",
                column: "ReaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_RoomId",
                table: "RoomAccesses",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_RoomId_CharacterId",
                table: "RoomAccesses",
                columns: new[] { "RoomId", "CharacterId" },
                unique: true,
                filter: "\"CharacterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAccesses_RoomId_ReaderUserId",
                table: "RoomAccesses",
                columns: new[] { "RoomId", "ReaderUserId" },
                unique: true,
                filter: "\"ReaderUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_DeletedByUserId",
                table: "Rooms",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_GameId",
                table: "Rooms",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_NextRoomId",
                table: "Rooms",
                column: "NextRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_PreviousRoomId",
                table: "Rooms",
                column: "PreviousRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricAccesses_RubricId",
                table: "RubricAccesses",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_RubricAccesses_UserId",
                table: "RubricAccesses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrics_BlogId",
                table: "Rubrics",
                column: "BlogId");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrics_DeletedByUserId",
                table: "Rubrics",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_SubscriberId",
                table: "Subscriptions",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TargetType_TargetId",
                table: "Subscriptions",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagGroupId",
                table: "Tags",
                column: "TagGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketResponses_AuthorId",
                table: "TicketResponses",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketResponses_TicketId",
                table: "TicketResponses",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AnswerAuthorId",
                table: "Tickets",
                column: "AnswerAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedModeratorId",
                table: "Tickets",
                column: "AssignedModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BanId",
                table: "Tickets",
                column: "BanId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TargetId",
                table: "Tickets",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UserId",
                table: "Tickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_WarningId",
                table: "Tickets",
                column: "WarningId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_CreatorId",
                table: "Tokens",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_DeletedByUserId",
                table: "Tokens",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_EntityId",
                table: "Tokens",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_UserId_Type",
                table: "Tokens",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_TopicEdits_EditorUserId",
                table: "TopicEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicEdits_TopicId",
                table: "TopicEdits",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_AuthorId",
                table: "Topics",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_BoardId_TopicNumber",
                table: "Topics",
                columns: new[] { "BoardId", "TopicNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_DeletedByUserId",
                table: "Topics",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_LastCommentId",
                table: "Topics",
                column: "LastCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_DeletedByUserId",
                table: "Uploads",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_TargetCharacterId",
                table: "Uploads",
                column: "TargetCharacterId",
                filter: "\"TargetCharacterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_TargetPostId",
                table: "Uploads",
                column: "TargetPostId",
                filter: "\"TargetPostId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_TargetUserId",
                table: "Uploads",
                column: "TargetUserId",
                filter: "\"TargetUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UserId",
                table: "Uploads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementTypeId",
                table: "UserAchievements",
                column: "AchievementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementTypeId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementTypeId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_EarnedUtc",
                table: "UserAchievements",
                columns: new[] { "UserId", "EarnedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAwards_AwardedByUserId",
                table: "UserAwards",
                column: "AwardedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAwards_AwardTypeId",
                table: "UserAwards",
                column: "AwardTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAwards_ContestSeriesId",
                table: "UserAwards",
                column: "ContestSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAwards_DeletedByUserId",
                table: "UserAwards",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAwards_UserId_AwardedUtc",
                table: "UserAwards",
                columns: new[] { "UserId", "AwardedUtc" },
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlacklists_BlockedUserId",
                table: "UserBlacklists",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlacklists_OwnerId_BlockedUserId",
                table: "UserBlacklists",
                columns: new[] { "OwnerId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserChatLinks_ChatId",
                table: "UserChatLinks",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatLinks_DeletedByUserId",
                table: "UserChatLinks",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChatLinks_UserId",
                table: "UserChatLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_UserId",
                table: "UserContacts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_AuthorId_TargetUserId",
                table: "UserEndorsements",
                columns: new[] { "AuthorId", "TargetUserId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_DeletedByUserId",
                table: "UserEndorsements",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_ModifiedByUserId",
                table: "UserEndorsements",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEndorsements_TargetUserId",
                table: "UserEndorsements",
                column: "TargetUserId",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_records_ip",
                table: "UserLoginRecords",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_records_user_date",
                table: "UserLoginRecords",
                columns: new[] { "UserId", "LoginUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsernameChangeRequests_ResolvedByUserId",
                table: "UsernameChangeRequests",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameChangeRequests_UserId",
                table: "UsernameChangeRequests",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistories_ApprovedByUserId",
                table: "UsernameHistories",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistories_OldUsername",
                table: "UsernameHistories",
                column: "OldUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsernameHistories_UserId",
                table: "UsernameHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileNotes_OwnerId_SubjectUserId",
                table: "UserProfileNotes",
                columns: new[] { "OwnerId", "SubjectUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfileNotes_SubjectUserId",
                table: "UserProfileNotes",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarUploadId",
                table: "Users",
                column: "AvatarUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_AuthorId",
                table: "Warnings",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_TargetUserId",
                table: "Warnings",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteTestimonials_AuthorId",
                table: "WebsiteTestimonials",
                column: "AuthorId",
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteTestimonials_DeletedByUserId",
                table: "WebsiteTestimonials",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteTestimonials_ModifiedByUserId",
                table: "WebsiteTestimonials",
                column: "ModifiedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bans_Users_AuthorId",
                table: "Bans",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bans_Users_TargetUserId",
                table: "Bans",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogAssistants_Blogs_BlogId",
                table: "BlogAssistants",
                column: "BlogId",
                principalTable: "Blogs",
                principalColumn: "BlogId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogAssistants_Users_UserId",
                table: "BlogAssistants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogBlacklists_Blogs_BlogId",
                table: "BlogBlacklists",
                column: "BlogId",
                principalTable: "Blogs",
                principalColumn: "BlogId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogBlacklists_Users_BlockedByUserId",
                table: "BlogBlacklists",
                column: "BlockedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlogBlacklists_Users_BlockedUserId",
                table: "BlogBlacklists",
                column: "BlockedUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Users_AuthorId",
                table: "Blogs",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Users_DeletedByUserId",
                table: "Blogs",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Blogs_Users_MentorId",
                table: "Blogs",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardModerators_Boards_BoardId",
                table: "BoardModerators",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardModerators_Users_UserId",
                table: "BoardModerators",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Topics_LastTopicId",
                table: "Boards",
                column: "LastTopicId",
                principalTable: "Topics",
                principalColumn: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Users_LastTopicAuthorId",
                table: "Boards",
                column: "LastTopicAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterAttributes_Characters_CharacterId",
                table: "CharacterAttributes",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterEdits_Characters_CharacterId",
                table: "CharacterEdits",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterEdits_Users_EditorUserId",
                table: "CharacterEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Games_GameId",
                table: "Characters",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_AuthorId",
                table: "Characters",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_DeletedByUserId",
                table: "Characters",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_Messages_LastMessageId",
                table: "Chats",
                column: "LastMessageId",
                principalTable: "Messages",
                principalColumn: "MessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_CommentEdits_Comments_CommentId",
                table: "CommentEdits",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommentEdits_Users_EditorUserId",
                table: "CommentEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            // FK_Comments_Topics_EntityId is deliberately absent, for the same reason as the
            // two token constraints above: Comment.EntityId is polymorphic and points at a
            // topic, a game, a blog or a publication. The constraint would reject every
            // comment that is not on a topic. Guarded by the same integration test.

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FundraisingGoals_Users_UpdatedByUserId",
                table: "FundraisingGoals",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameAssistants_Games_GameId",
                table: "GameAssistants",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameAssistants_Users_UserId",
                table: "GameAssistants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameBlacklists_Games_GameId",
                table: "GameBlacklists",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameBlacklists_Users_BlockedByUserId",
                table: "GameBlacklists",
                column: "BlockedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameBlacklists_Users_BlockedUserId",
                table: "GameBlacklists",
                column: "BlockedUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameReviews_Games_GameId",
                table: "GameReviews",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "GameId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameReviews_Users_AuthorId",
                table: "GameReviews",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameReviews_Users_DeletedByUserId",
                table: "GameReviews",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameReviews_Users_ModifiedByUserId",
                table: "GameReviews",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_DeletedByUserId",
                table: "Games",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_MasterId",
                table: "Games",
                column: "MasterId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_MentorId",
                table: "Games",
                column: "MentorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalChatEventParticipants_GlobalChatEvents_GlobalChatEven~",
                table: "GlobalChatEventParticipants",
                column: "GlobalChatEventId",
                principalTable: "GlobalChatEvents",
                principalColumn: "GlobalChatEventId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalChatEventParticipants_Users_UserId",
                table: "GlobalChatEventParticipants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalChatEvents_Users_CreatedByUserId",
                table: "GlobalChatEvents",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Users_DeletedByUserId",
                table: "Likes",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Users_UserId",
                table: "Likes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageEdits_Messages_MessageId",
                table: "MessageEdits",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "MessageId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageEdits_Users_EditorUserId",
                table: "MessageEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_UserId",
                table: "Messages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModeratedProfileNotes_Users_AuthorId",
                table: "ModeratedProfileNotes",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModeratedProfileNotes_Users_UserId",
                table: "ModeratedProfileNotes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadCategories_Users_AuthorId",
                table: "NotepadCategories",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadCategories_Users_DeletedByUserId",
                table: "NotepadCategories",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadEntries_Users_AuthorId",
                table: "NotepadEntries",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotepadEntries_Users_DeletedByUserId",
                table: "NotepadEntries",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PostEdits_Posts_PostId",
                table: "PostEdits",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "PostId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostEdits_Users_EditorUserId",
                table: "PostEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostPendencies_Rooms_RoomId",
                table: "PostPendencies",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostPendencies_Users_CreatedById",
                table: "PostPendencies",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostPendencies_Users_WaitingForUserId",
                table: "PostPendencies",
                column: "WaitingForUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PostReviews_Posts_PostId",
                table: "PostReviews",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "PostId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReviews_Users_AuthorId",
                table: "PostReviews",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReviews_Users_DeletedByUserId",
                table: "PostReviews",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReviews_Users_ModifiedByUserId",
                table: "PostReviews",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReviews_Users_PostAuthorId",
                table: "PostReviews",
                column: "PostAuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Rooms_RoomId",
                table: "Posts",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Users_AuthorId",
                table: "Posts",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Users_DeletedByUserId",
                table: "Posts",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Rubrics_RubricId",
                table: "Publications",
                column: "RubricId",
                principalTable: "Rubrics",
                principalColumn: "RubricId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Users_AuthorId",
                table: "Publications",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Users_DeletedByUserId",
                table: "Publications",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Publications_Users_ModifiedByUserId",
                table: "Publications",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomAccesses_Rooms_RoomId",
                table: "RoomAccesses",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomAccesses_Users_ReaderUserId",
                table: "RoomAccesses",
                column: "ReaderUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Users_DeletedByUserId",
                table: "Rooms",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RubricAccesses_Rubrics_RubricId",
                table: "RubricAccesses",
                column: "RubricId",
                principalTable: "Rubrics",
                principalColumn: "RubricId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RubricAccesses_Users_UserId",
                table: "RubricAccesses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rubrics_Users_DeletedByUserId",
                table: "Rubrics",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_SubscriberId",
                table: "Subscriptions",
                column: "SubscriberId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketResponses_Tickets_TicketId",
                table: "TicketResponses",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "TicketId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketResponses_Users_AuthorId",
                table: "TicketResponses",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AnswerAuthorId",
                table: "Tickets",
                column: "AnswerAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssignedModeratorId",
                table: "Tickets",
                column: "AssignedModeratorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_TargetId",
                table: "Tickets",
                column: "TargetId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_UserId",
                table: "Tickets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Warnings_WarningId",
                table: "Tickets",
                column: "WarningId",
                principalTable: "Warnings",
                principalColumn: "WarningId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Users_CreatorId",
                table: "Tokens",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Users_DeletedByUserId",
                table: "Tokens",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Users_UserId",
                table: "Tokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicEdits_Topics_TopicId",
                table: "TopicEdits",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "TopicId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TopicEdits_Users_EditorUserId",
                table: "TopicEdits",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Users_AuthorId",
                table: "Topics",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Users_DeletedByUserId",
                table: "Topics",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_DeletedByUserId",
                table: "Uploads",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_TargetUserId",
                table: "Uploads",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_UserId",
                table: "Uploads",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Users_LastTopicAuthorId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_AuthorId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_DeletedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_DeletedByUserId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_MasterId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_MentorId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_GlobalChatEvents_Users_CreatedByUserId",
                table: "GlobalChatEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_UserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_AuthorId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_DeletedByUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Users_DeletedByUserId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_AuthorId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_DeletedByUserId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_DeletedByUserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_TargetUserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_UserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Boards_BoardId",
                table: "Topics");


            migrationBuilder.DropForeignKey(
                name: "FK_Chats_Messages_LastMessageId",
                table: "Chats");

            migrationBuilder.DropTable(
                name: "BlogAssistants");

            migrationBuilder.DropTable(
                name: "BlogBlacklists");

            migrationBuilder.DropTable(
                name: "BoardModerators");

            migrationBuilder.DropTable(
                name: "CharacterAttributes");

            migrationBuilder.DropTable(
                name: "CharacterEdits");

            migrationBuilder.DropTable(
                name: "CommentEdits");

            migrationBuilder.DropTable(
                name: "FundraisingGoals");

            migrationBuilder.DropTable(
                name: "GameAssistants");

            migrationBuilder.DropTable(
                name: "GameBlacklists");

            migrationBuilder.DropTable(
                name: "GameReviews");

            migrationBuilder.DropTable(
                name: "GameTags");

            migrationBuilder.DropTable(
                name: "GlobalChatEventParticipants");

            migrationBuilder.DropTable(
                name: "Likes");

            migrationBuilder.DropTable(
                name: "MessageEdits");

            migrationBuilder.DropTable(
                name: "ModeratedProfileNotes");

            migrationBuilder.DropTable(
                name: "NotepadEntries");

            migrationBuilder.DropTable(
                name: "PendingRegistrations");

            migrationBuilder.DropTable(
                name: "PeriodDigestTopics");

            migrationBuilder.DropTable(
                name: "PostEdits");

            migrationBuilder.DropTable(
                name: "PostPendencies");

            migrationBuilder.DropTable(
                name: "PostReviews");

            migrationBuilder.DropTable(
                name: "Publications");

            migrationBuilder.DropTable(
                name: "RoomAccesses");

            migrationBuilder.DropTable(
                name: "RubricAccesses");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TicketResponses");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "TopicEdits");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserAwards");

            migrationBuilder.DropTable(
                name: "UserBlacklists");

            migrationBuilder.DropTable(
                name: "UserChatLinks");

            migrationBuilder.DropTable(
                name: "UserContacts");

            migrationBuilder.DropTable(
                name: "UserEndorsements");

            migrationBuilder.DropTable(
                name: "UserLoginRecords");

            migrationBuilder.DropTable(
                name: "UsernameChangeRequests");

            migrationBuilder.DropTable(
                name: "UsernameHistories");

            migrationBuilder.DropTable(
                name: "UserProfileNotes");

            migrationBuilder.DropTable(
                name: "WebsiteTestimonials");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "NotepadCategories");

            migrationBuilder.DropTable(
                name: "Rubrics");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "AchievementTypes");

            migrationBuilder.DropTable(
                name: "AwardTypes");

            migrationBuilder.DropTable(
                name: "ContestSeries");

            migrationBuilder.DropTable(
                name: "TagGroups");

            migrationBuilder.DropTable(
                name: "Blogs");

            migrationBuilder.DropTable(
                name: "Bans");

            migrationBuilder.DropTable(
                name: "Warnings");

            migrationBuilder.DropTable(
                name: "AchievementCategories");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Uploads");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Chats");

            migrationBuilder.DropTable(
                name: "GlobalChatEvents");
        }
    }
}
