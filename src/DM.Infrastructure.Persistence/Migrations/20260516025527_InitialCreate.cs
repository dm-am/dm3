using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace DM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trigram extension powers fuzzy username/title search
            // (EF.Functions.TrigramsSimilarity -> pg similarity()). Created
            // up-front, idempotently, so Database.Migrate() provisions it on a
            // fresh database (matches the integration-test fixture).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

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
                name: "OutboxEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.Id);
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
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
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
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentTopicTitle = table.Column<string>(type: "text", nullable: true),
                    LastCommentTopicNumber = table.Column<int>(type: "integer", nullable: true),
                    LastCommentAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    LastReminderUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
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
                    // CHECK: exactly one typed target column is non-null.
                    table.CheckConstraint(
                        "CK_RoomAccesses_TypedTarget",
                        "(\"CharacterId\" IS NOT NULL AND \"ReaderUserId\" IS NULL) OR " +
                        "(\"CharacterId\" IS NULL AND \"ReaderUserId\" IS NOT NULL)");
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
                    // NOTE: FK_Tokens_Blogs_EntityId and FK_Tokens_Games_EntityId removed —
                    // Token.EntityId is polymorphic (a GameId, a BlogId or nothing, depending
                    // on Type). Both constraints on one column means a value would have to
                    // exist in Blogs AND Games simultaneously, so every invitation INSERT
                    // failed. Same reasoning as FK_Comments_Topics_EntityId below.
                    // Referential integrity is maintained by application logic.
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
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
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
                    // CHECK: exactly one typed target column is non-null AND matches Type.
                    // UploadType: UserAvatar=1, CharacterAvatar=2, PostAttachment=3.
                    table.CheckConstraint(
                        "CK_Uploads_TypedTarget",
                        "(\"Type\" = 1 AND \"TargetUserId\" IS NOT NULL AND \"TargetCharacterId\" IS NULL AND \"TargetPostId\" IS NULL) OR " +
                        "(\"Type\" = 2 AND \"TargetCharacterId\" IS NOT NULL AND \"TargetUserId\" IS NULL AND \"TargetPostId\" IS NULL) OR " +
                        "(\"Type\" = 3 AND \"TargetPostId\" IS NOT NULL AND \"TargetUserId\" IS NULL AND \"TargetCharacterId\" IS NULL)");
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
                    table.ForeignKey(
                        name: "FK_FundraisingGoals_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            // SEED: TagGroups / Tags / Boards / Users / Chats / Topics.
            // NOT via DbContext.HasData — when the migration is regenerated
            // these blocks are restored from the old migration's git history.
            migrationBuilder.InsertData(
                table: "TagGroups",
                columns: new[] { "TagGroupId", "Title", "Description", "SortOrder" },
                values: new object[,]
                {
                    { Guid.Parse("00000000-0000-0000-0000-000000000000"), "Система", "Ролевая система или набор правил, по которым ведется игра", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000001"), "Жанр", "Жанр и сеттинг игрового мира", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000002"), "Формат игры", "Тип игрового процесса и взаимодействия между участниками", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000003"), "Формат постов", "Стиль и объем игровых постов", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000004"), "Темп", "Ожидаемая скорость игры и частота постов", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000005"), "Ограничения", "Особые требования и ограничения для участников", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000006"), "Новички", "Игры от новичков и для новичков", 6 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000007"), "Деликатный контент", "Контент, требующий осознанного согласия участников", 7 }
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "TagId", "ShortId", "TagGroupId", "Title", "Description", "SortOrder" },
                values: new object[,]
                {
                    // === System ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000001"), 1, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Black Bird Pie", "Простая система с кубиком d6", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000002"), 2, Guid.Parse("00000000-0000-0000-0000-000000000000"), "D&D", "Dungeons & Dragons — все редакции классической ролевой системы", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000003"), 3, Guid.Parse("00000000-0000-0000-0000-000000000000"), "D&D 5e", "Dungeons & Dragons 5th Edition", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000004"), 4, Guid.Parse("00000000-0000-0000-0000-000000000000"), "D100", "Системы на основе процентного броска", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000005"), 5, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Dawn of Worlds", "Система для совместного создания мира", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000006"), 6, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Fallout", "Адаптация сеттинга Fallout", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000007"), 7, Guid.Parse("00000000-0000-0000-0000-000000000000"), "FATAL", "Без комментариев", 6 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000008"), 8, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Fate", "Нарративная система с аспектами и фейт-пойнтами", 7 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000009"), 9, Guid.Parse("00000000-0000-0000-0000-000000000000"), "FUDGE", "Универсальный движок для реализации практически любого концепта", 8 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000a"), 10, Guid.Parse("00000000-0000-0000-0000-000000000000"), "GURPS", "Универсальная система на базе броска 3d6 vs Сложность", 9 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000b"), 11, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Interlock", "Система от R. Talsorian Games (Cyberpunk 2020 и другие)", 10 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000c"), 12, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Microscope", "Система для создания эпических историй", 11 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000d"), 13, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Pathfinder 1e", "Pathfinder первой редакции", 12 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000e"), 14, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Pathfinder 2e", "Pathfinder второй редакции", 13 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000f"), 15, Guid.Parse("00000000-0000-0000-0000-000000000000"), "PbtA", "Нарративные системы на базе 2d6 vs Сложность", 14 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000010"), 16, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Risus", "Минималистичная комедийная система", 15 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000011"), 17, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Savage Worlds", "Легковесная универсальная система — Fast! Furious! Fun!", 16 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000012"), 18, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Starfinder 1e", "Sci-fi спин-офф Pathfinder", 17 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000013"), 19, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Starfinder 2e", "Starfinder второй редакции", 18 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000014"), 20, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Warhammer", "Системы по вселенной Warhammer", 19 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000015"), 21, Guid.Parse("00000000-0000-0000-0000-000000000000"), "World of Darkness", "Мир Тьмы — вампиры, оборотни, маги", 20 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000016"), 22, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Авторская", "Оригинальная система от мастера игры", 21 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000017"), 23, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Мафия", "Психологическая детективная командная игра", 22 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000018"), 24, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Словеска", "Игра без формальной системы правил", 23 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000019"), 25, Guid.Parse("00000000-0000-0000-0000-000000000000"), "Эра Водолея", "Отечественная система ролевых игр", 24 },

                    // === Genre ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000001a"), 26, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Альтернативная история", "Переосмысление исторических событий", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001b"), 27, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Боевик", "Акцент на экшн и сражениях", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001c"), 28, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Детектив", "Расследования и разгадывание тайн", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001d"), 29, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Зомби", "Зомби-апокалипсис и выживание", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001e"), 30, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Историческое", "Действие в реальную историческую эпоху", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000001f"), 31, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Киберпанк", "Высокие технологии, низкий уровень жизни", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000020"), 32, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Комедия", "Юмор и абсурдные ситуации", 6 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000021"), 33, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Космоопера", "Эпические приключения в космосе", 7 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000022"), 34, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Мистика", "Сверхъестественные элементы и тайны", 8 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000023"), 35, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Наши дни", "Современный реалистичный сеттинг", 9 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000024"), 36, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Постапокалипсис", "Мир после катастрофы", 10 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000025"), 37, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Психоделика", "Сюрреалистичные и необычные миры", 11 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000026"), 38, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Стимпанк", "Паровые технологии и викторианская эстетика", 12 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000027"), 39, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Триллер", "Напряжение и саспенс", 13 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000028"), 40, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Трэш", "Нарочито нелепый и провокационный контент", 14 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000029"), 41, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Ужасы", "Хоррор и атмосфера страха", 15 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002a"), 42, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Фантастика", "Научная фантастика и будущее", 16 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002b"), 43, Guid.Parse("00000000-0000-0000-0000-000000000001"), "Фэнтези", "Магия, мечи и волшебные миры", 17 },

                    // === Game format ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000002c"), 44, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Dungeon Crawl", "Исследование подземелий и сражения с монстрами", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002d"), 45, Guid.Parse("00000000-0000-0000-0000-000000000002"), "PvP", "Противостояние между игроками", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002e"), 46, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Выживание", "Борьба за выживание в суровых условиях", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000002f"), 47, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Песочница", "Открытый мир без сюжетных ограничений", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000030"), 48, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Стратегия", "Управление ресурсами и принятие глобальных решений", 4 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000031"), 49, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Сюжетная", "Фокус на развитии истории", 5 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000032"), 50, Guid.Parse("00000000-0000-0000-0000-000000000002"), "Тактика", "Тактические бои и позиционирование", 6 },

                    // === Post format ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000033"), 51, Guid.Parse("00000000-0000-0000-0000-000000000003"), "Короткопост", "Короткие посты в 1-3 абзаца", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000034"), 52, Guid.Parse("00000000-0000-0000-0000-000000000003"), "Литературная", "Развернутые литературные посты", 1 },

                    // === Pace ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000035"), 53, Guid.Parse("00000000-0000-0000-0000-000000000004"), "Неторопливый", "Посты раз в несколько дней", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000036"), 54, Guid.Parse("00000000-0000-0000-0000-000000000004"), "Скоростной", "Несколько постов в день", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003e"), 62, Guid.Parse("00000000-0000-0000-0000-000000000004"), "Сухие сезоны", "Возможны продолжительные периоды без постов", 2 },

                    // === Restrictions ===
                    { Guid.Parse("00000000-0000-0000-0000-000000000037"), 55, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Без мата", "Нецензурная лексика запрещена", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000038"), 56, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Без насилия", "Минимум жестокости и крови", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000039"), 57, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Grammar Nazi", "Повышенные требования к грамотности", 2 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003a"), 58, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Для своих", "Игра для знакомой компании", 3 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003b"), 59, Guid.Parse("00000000-0000-0000-0000-000000000005"), "Обязателен мессенджер", "Обсуждение игровых вопросов во внешнем мессенджере", 4 },

                    // === Newcomers ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000003c"), 60, Guid.Parse("00000000-0000-0000-0000-000000000006"), "Для новичков", "Игра подходит для начинающих", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000003d"), 61, Guid.Parse("00000000-0000-0000-0000-000000000006"), "Мастер-новичок", "Мастер игры — начинающий", 1 },

                    // === Sensitive content ===
                    { Guid.Parse("00000000-0000-0000-0000-00000000003f"), 63, Guid.Parse("00000000-0000-0000-0000-000000000007"), "ERP", "Erotic Role-Play: [tipimg:/images/erp-tooltip.gif]эротические сцены[/tipimg] как основа игрового процесса", 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000040"), 64, Guid.Parse("00000000-0000-0000-0000-000000000007"), "Шок-контент", "Чернуха, максимально шокирующий и отталкивающий контент без ограничений", 1 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000041"), 65, Guid.Parse("00000000-0000-0000-0000-000000000007"), "Острые темы", "Игра затрагивает спорные или чувствительные социальные темы", 2 }
                });

            migrationBuilder.InsertData(
                table: "Boards",
                columns: new[] { "BoardId", "Title", "Alias", "Description", "Order", "ViewPolicy", "CreateTopicPolicy", "TopicsCount", "CommentsCount" },
                values: new object[,]
                {
                    { Guid.Parse("00000000-0000-0000-0000-000000000001"), "Общий", "general", "Жизнь сообщества и решения администрации", 1, 64, 32, 1, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000002"), "Игровые системы", "game-systems", "Обсуждение правил и помощь в выборе системы", 2, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000003"), "Поиск мастера и игроков", "looking-for-group", "Набор игроков в игру или поиск мастера", 3, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000004"), "Котел идей", "ideas", "Обкатка задумок и поиск единомышленников", 4, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000005"), "Конкурсы", "contests", "Литературные и творческие состязания", 5, 64, 4, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000006"), "Под столом", "off-topic", "Музыка, книги, кино, мемы и все остальное", 6, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000007"), "Неролевые игры", "forum-games", "Словесные игры, ассоциации и прочие развлечения", 7, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000008"), "Улучшение сайта", "improvements", "Идеи и предложения по развитию сайта", 8, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-000000000009"), "Ошибки", "bugs", "Сообщения об ошибках на сайте", 9, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000a"), "Для новичков", "newbies", "Руководства, ответы на вопросы и помощь новичкам", 10, 64, 32, 0, 0 },
                    { Guid.Parse("00000000-0000-0000-0000-00000000000b"), "Новости проекта", "news", "Официальные новости, обновления и статистика", 11, 64, 4, 0, 0 }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Username", "Email", "CreatedUtc", "Role", "AccessPolicy", "Salt", "PasswordHash", "PasswordHashVersion", "RatingDisabled", "QualityRating", "QuantityRating", "IsRemoved", "Gender", "ShowBirthday" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // SystemUser.Id
                    "Робот-Администратор", // SystemUser.Username
                    "system@dm.local", // SystemUser.Email
                    new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), // CreatedUtc
                    6, // UserRole.System
                    0, // AccessPolicy.NotSpecified
                    "", // Salt (not used, cannot login)
                    "", // PasswordHash (not used, cannot login)
                    0, // PasswordHashVersion
                    true, // RatingDisabled
                    0, // QualityRating
                    0, // QuantityRating
                    false, // IsRemoved
                    0, // Gender.NotSpecified
                    false // ShowBirthday
                });

            migrationBuilder.InsertData(
                table: "Chats",
                columns: new[] { "ChatId", "Type", "Title", "RoomId", "LastMessageId" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // Chat.GlobalChatId
                    2, // ChatType.Global
                    "Глобальный чат", // Title
                    null, // RoomId
                    null // LastMessageId
                });

            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "TopicId", "BoardId", "TopicNumber", "AuthorId", "CreatedUtc", "Title", "Text", "IsAttached", "IsClosed", "CommentCount", "LastCommentId", "IsRemoved", "DeletedByUserId", "DeletedUtc" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // TopicId
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // BoardId (the "Общий" board)
                    1, // TopicNumber
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // AuthorId (Robot)
                    new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), // CreatedUtc
                    "Отзывы о ДМ", // Title
                    "Ваши отзывы отсюда попадают (после минимального анализа на нарушения правил) прямиком на главную.", // Text
                    true, // IsAttached (pinned)
                    false, // IsClosed
                    0, // CommentCount
                    null, // LastCommentId
                    false, // IsRemoved
                    null, // DeletedByUserId
                    null // DeletedUtc
                });

            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "TopicId", "BoardId", "TopicNumber", "AuthorId", "CreatedUtc", "Title", "Text", "IsAttached", "IsClosed", "CommentCount", "LastCommentId", "IsRemoved", "DeletedByUserId", "DeletedUtc" },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000100"), // TopicId (well-known ID)
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // BoardId (the "Общий" board)
                    2, // TopicNumber
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), // AuthorId (Robot)
                    new DateTimeOffset(2020, 1, 1, 0, 0, 1, TimeSpan.Zero), // CreatedUtc (+1 sec to be "newer")
                    "Обсуждение действий администрации", // Title
                    "Здесь можно обсудить решения модераторов и администрации. Конструктивная критика приветствуется.", // Text
                    true, // IsAttached (pinned)
                    false, // IsClosed
                    0, // CommentCount
                    null, // LastCommentId
                    false, // IsRemoved
                    null, // DeletedByUserId
                    null // DeletedUtc
                });

            // SEED: catalog of 13 achievement categories. A category = SSOT for
            // (Title, Description, IconName, Metric, SortOrder, IsActive)
            // one chain of tiers. One Metric = one category (UNIQUE).
            // Description spells out "what exactly is counted" — needed because
            // a numeric threshold alone does not explain the nuances
            // (positive reviews, drops vs exiles, etc.).
            // Ordering (SortOrder):
            //   1. Выслуга лет, 2. Игровые посты, 3. Рейтинг,
            //   4. Игры в роли ведущего, 5. Игры в роли игрока,
            //   6. Блоги в роли ведущего, 7. Публикации, 8. Топики,
            //   9. Комментарии, 10. Глобальный чат, 11. Лайки,
            //   12. Дропы, 13. Баны.
            // "Игровые посты" comes before "Рейтинг" — the rating derives from post quality.
            // "Лайки" is a positive reaction sum, placed before the negative chains (Дропы, Баны).
            migrationBuilder.InsertData(
                table: "AchievementCategories",
                columns: new[] { "AchievementCategoryId", "Code", "Title", "Description", "IconName", "Metric", "SortOrder", "IsActive" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0003-000000000001"), "days_since_registration", "Выслуга лет",            "Время с момента регистрации на сайте.",                                                                                "hourglass",      2,  1, true },
                    { new Guid("00000000-0000-0000-0003-000000000002"), "game_posts_authored",     "Игровые посты",          "Игровые посты в активных играх. Считаются все, включая удаленные игры.",                                              "scroll-quill",   1,  2, true },
                    { new Guid("00000000-0000-0000-0003-000000000003"), "post_review_score_sum",   "Рейтинг",                "Сумма положительных оценок твоих игровых постов. Отрицательные оценки рейтинг не уменьшают.",                          "laurels",        3,  3, true },
                    { new Guid("00000000-0000-0000-0003-000000000004"), "games_hosted",            "Игры в роли ведущего",   "Игры, где ты мастер или ассистент.",                                                                                   "scepter",        4,  4, true },
                    { new Guid("00000000-0000-0000-0003-000000000005"), "games_played",            "Игры в роли игрока",     "Игры, где у тебя есть активный или бывший персонаж.",                                                                  "sword",          5,  5, true },
                    { new Guid("00000000-0000-0000-0003-000000000006"), "blogs_hosted",            "Блоги в роли ведущего",  "Блоги, где ты автор или ассистент.",                                                                                   "book",           6,  6, true },
                    { new Guid("00000000-0000-0000-0003-000000000007"), "publications_authored",  "Публикации",             "Статьи в блогах. Черновики тоже считаются.",                                                                           "papers",        12,  7, true },
                    { new Guid("00000000-0000-0000-0003-000000000008"), "topics_authored",         "Топики",                 "Форумные топики, которые ты создал.",                                                                                  "stabbed-note",   7,  8, true },
                    { new Guid("00000000-0000-0000-0003-000000000009"), "comments_authored",       "Комментарии",            "Все комментарии: форум, блоги, игры, публикации.",                                                                     "discussion",     8,  9, true },
                    { new Guid("00000000-0000-0000-0003-00000000000a"), "global_chat_messages",    "Глобальный чат",         "Сообщения в глобальном чате сайта.",                                                                                        "talk",           9, 10, true },
                    { new Guid("00000000-0000-0000-0003-00000000000b"), "likes_received",          "Лайки",                  "Лайки на топиках, публикациях, комментариях и сообщениях чата. Игровые посты учитываются через \"Рейтинг\".",            "heart-organ",   13, 11, true },
                    { new Guid("00000000-0000-0000-0003-00000000000c"), "game_drops",              "Дропы",                  "Игры, которые ты покинул добровольно. Смерть персонажа и изгнание мастером не считаются.",                            "walking-boot",  11, 12, true },
                    { new Guid("00000000-0000-0000-0003-00000000000d"), "bans_received",           "Баны",                   "Баны, полученные от модерации.",                                                                                       "plastic-duck",  10, 13, true },
                });

            // SEED: 13 chains × 4 tiers = 52 types. A thin record: only
            // Code/Title/Threshold/Tier/CategoryId. Everything else lives on the category.
            // Titles are noun phrases and set idioms; within a chain the levels
            // grow in meaning (reach/scale/duration), and the level constructions
            // do not repeat. Negativity is allowed only in the joke chains:
            // Дропы is a disappearance arc, Баны is a duck coming-of-age arc.
            // Выслуга лет: thresholds are ceil(N×365.25) — guarantees triggering
            // on or after the anniversary day (no leap-year off-by-one).
            migrationBuilder.InsertData(
                table: "AchievementTypes",
                columns: new[] { "AchievementTypeId", "Code", "Title", "Threshold", "Tier", "AchievementCategoryId" },
                values: new object[,]
                {
                    // ─── Выслуга лет ─── (366/1827/3653/5479 days = 1/5/10/15 years, leap-aware)
                    { new Guid("00000000-0000-0000-0002-000000000005"), "DAYS_366",       "Поселенец",        366,    1, new Guid("00000000-0000-0000-0003-000000000001") },
                    { new Guid("00000000-0000-0000-0002-000000000006"), "DAYS_1827",      "Старожил",         1827,   2, new Guid("00000000-0000-0000-0003-000000000001") },
                    { new Guid("00000000-0000-0000-0002-000000000007"), "DAYS_3653",      "Ветеран",          3653,   3, new Guid("00000000-0000-0000-0003-000000000001") },
                    { new Guid("00000000-0000-0000-0002-000000000008"), "DAYS_5479",      "Древний",          5479,   4, new Guid("00000000-0000-0000-0003-000000000001") },
                    // ─── Игровые посты ───
                    { new Guid("00000000-0000-0000-0002-000000000001"), "POSTS_100",      "Простые начала",         100,    1, new Guid("00000000-0000-0000-0003-000000000002") },
                    { new Guid("00000000-0000-0000-0002-000000000002"), "POSTS_500",      "Продолжение следует",    500,    2, new Guid("00000000-0000-0000-0003-000000000002") },
                    { new Guid("00000000-0000-0000-0002-000000000003"), "POSTS_2000",     "Долгая партия",          2000,   3, new Guid("00000000-0000-0000-0003-000000000002") },
                    { new Guid("00000000-0000-0000-0002-000000000004"), "POSTS_5000",     "Приключение в жизнь",    5000,   4, new Guid("00000000-0000-0000-0003-000000000002") },
                    // ─── Рейтинг ───
                    { new Guid("00000000-0000-0000-0002-000000000009"), "RATING_100",     "Подающий надежды",       100,    1, new Guid("00000000-0000-0000-0003-000000000003") },
                    { new Guid("00000000-0000-0000-0002-00000000000a"), "RATING_250",     "Видный талант",          250,    2, new Guid("00000000-0000-0000-0003-000000000003") },
                    { new Guid("00000000-0000-0000-0002-00000000000b"), "RATING_500",     "Опытный зубр",           500,    3, new Guid("00000000-0000-0000-0003-000000000003") },
                    { new Guid("00000000-0000-0000-0002-00000000000c"), "RATING_1000",    "Мастодонт-аксакал",      1000,   4, new Guid("00000000-0000-0000-0003-000000000003") },
                    // ─── Игры в роли ведущего ───
                    { new Guid("00000000-0000-0000-0002-00000000000d"), "HOST_3",         "Подмастерье",            3,      1, new Guid("00000000-0000-0000-0003-000000000004") },
                    { new Guid("00000000-0000-0000-0002-00000000000e"), "HOST_10",        "Мастер",                 10,     2, new Guid("00000000-0000-0000-0003-000000000004") },
                    { new Guid("00000000-0000-0000-0002-00000000000f"), "HOST_30",        "Грандмастер",            30,     3, new Guid("00000000-0000-0000-0003-000000000004") },
                    { new Guid("00000000-0000-0000-0002-000000000010"), "HOST_100",       "Архитектор миров",       100,    4, new Guid("00000000-0000-0000-0003-000000000004") },
                    // ─── Игры в роли игрока ───
                    { new Guid("00000000-0000-0000-0002-000000000011"), "PLAY_5",         "Искатель",               5,      1, new Guid("00000000-0000-0000-0003-000000000005") },
                    { new Guid("00000000-0000-0000-0002-000000000012"), "PLAY_20",        "Авантюрист",             20,     2, new Guid("00000000-0000-0000-0003-000000000005") },
                    { new Guid("00000000-0000-0000-0002-000000000013"), "PLAY_100",       "Герой",                  100,    3, new Guid("00000000-0000-0000-0003-000000000005") },
                    { new Guid("00000000-0000-0000-0002-000000000014"), "PLAY_500",       "Легенда",                500,    4, new Guid("00000000-0000-0000-0003-000000000005") },
                    // ─── Блоги в роли ведущего ───
                    { new Guid("00000000-0000-0000-0002-000000000015"), "BLOGS_1",        "Свежий взгляд",          1,      1, new Guid("00000000-0000-0000-0003-000000000006") },
                    { new Guid("00000000-0000-0000-0002-000000000016"), "BLOGS_5",        "Небольшая подборка",     5,      2, new Guid("00000000-0000-0000-0003-000000000006") },
                    { new Guid("00000000-0000-0000-0002-000000000017"), "BLOGS_15",       "Именная коллекция",      15,     3, new Guid("00000000-0000-0000-0003-000000000006") },
                    { new Guid("00000000-0000-0000-0002-000000000018"), "BLOGS_50",       "Библиотека",             50,     4, new Guid("00000000-0000-0000-0003-000000000006") },
                    // ─── Публикации ───
                    { new Guid("00000000-0000-0000-0002-00000000002d"), "PUBS_5",         "Проба пера",             5,      1, new Guid("00000000-0000-0000-0003-000000000007") },
                    { new Guid("00000000-0000-0000-0002-00000000002e"), "PUBS_25",        "Мысли вслух",            25,     2, new Guid("00000000-0000-0000-0003-000000000007") },
                    { new Guid("00000000-0000-0000-0002-00000000002f"), "PUBS_100",       "Постоянная рубрика",     100,    3, new Guid("00000000-0000-0000-0003-000000000007") },
                    { new Guid("00000000-0000-0000-0002-000000000030"), "PUBS_500",       "Без строчки ни дня",     500,    4, new Guid("00000000-0000-0000-0003-000000000007") },
                    // ─── Топики ───
                    { new Guid("00000000-0000-0000-0002-000000000019"), "TOPICS_5",       "Повод для обсуждения",   5,      1, new Guid("00000000-0000-0000-0003-000000000008") },
                    { new Guid("00000000-0000-0000-0002-00000000001a"), "TOPICS_25",      "Занятные темы",          25,     2, new Guid("00000000-0000-0000-0003-000000000008") },
                    { new Guid("00000000-0000-0000-0002-00000000001b"), "TOPICS_100",     "Дневная повестка",       100,    3, new Guid("00000000-0000-0000-0003-000000000008") },
                    { new Guid("00000000-0000-0000-0002-00000000001c"), "TOPICS_500",     "На целый раздел",        500,    4, new Guid("00000000-0000-0000-0003-000000000008") },
                    // ─── Комментарии ───
                    { new Guid("00000000-0000-0000-0002-00000000001d"), "COMMENTS_100",   "Свои пять копеек",       100,    1, new Guid("00000000-0000-0000-0003-000000000009") },
                    { new Guid("00000000-0000-0000-0002-00000000001e"), "COMMENTS_500",   "Живое участие",          500,    2, new Guid("00000000-0000-0000-0003-000000000009") },
                    { new Guid("00000000-0000-0000-0002-00000000001f"), "COMMENTS_2000",  "В гуще событий",         2000,   3, new Guid("00000000-0000-0000-0003-000000000009") },
                    { new Guid("00000000-0000-0000-0002-000000000020"), "COMMENTS_10000", "Всегда есть что сказать", 10000, 4, new Guid("00000000-0000-0000-0003-000000000009") },
                    // ─── Глобальный чат ───
                    { new Guid("00000000-0000-0000-0002-000000000021"), "CHAT_500",       "Прохожий",               500,    1, new Guid("00000000-0000-0000-0003-00000000000a") },
                    { new Guid("00000000-0000-0000-0002-000000000022"), "CHAT_5000",      "Свой человек",           5000,   2, new Guid("00000000-0000-0000-0003-00000000000a") },
                    { new Guid("00000000-0000-0000-0002-000000000023"), "CHAT_25000",     "Чат-завсегдатай",        25000,  3, new Guid("00000000-0000-0000-0003-00000000000a") },
                    { new Guid("00000000-0000-0000-0002-000000000024"), "CHAT_100000",    "Вечный онлайн",          100000, 4, new Guid("00000000-0000-0000-0003-00000000000a") },
                    // ─── Лайки ───
                    { new Guid("00000000-0000-0000-0002-000000000031"), "LIKES_25",       "В узких кругах",         25,     1, new Guid("00000000-0000-0000-0003-00000000000b") },
                    { new Guid("00000000-0000-0000-0002-000000000032"), "LIKES_100",      "Душа компании",          100,    2, new Guid("00000000-0000-0000-0003-00000000000b") },
                    { new Guid("00000000-0000-0000-0002-000000000033"), "LIKES_500",      "Народный любимец",       500,    3, new Guid("00000000-0000-0000-0003-00000000000b") },
                    { new Guid("00000000-0000-0000-0002-000000000034"), "LIKES_2000",     "Первый в сердцах",       2000,   4, new Guid("00000000-0000-0000-0003-00000000000b") },
                    // ─── Дропы ───
                    { new Guid("00000000-0000-0000-0002-000000000029"), "DROPS_1",        "Перекати-поле",          1,      1, new Guid("00000000-0000-0000-0003-00000000000c") },
                    { new Guid("00000000-0000-0000-0002-00000000002a"), "DROPS_3",        "Беглец",                 3,      2, new Guid("00000000-0000-0000-0003-00000000000c") },
                    { new Guid("00000000-0000-0000-0002-00000000002b"), "DROPS_10",       "Дезертир",               10,     3, new Guid("00000000-0000-0000-0003-00000000000c") },
                    { new Guid("00000000-0000-0000-0002-00000000002c"), "DROPS_30",       "Пропавший без вести",    30,     4, new Guid("00000000-0000-0000-0003-00000000000c") },
                    // ─── Баны ─── (duck coming-of-age arc: from eggshell to drake)
                    { new Guid("00000000-0000-0000-0002-000000000025"), "BANS_1",         "Яйцо с характером",      1,  1, new Guid("00000000-0000-0000-0003-00000000000d") },
                    { new Guid("00000000-0000-0000-0002-000000000026"), "BANS_3",         "Выпавший из гнезда",     3,  2, new Guid("00000000-0000-0000-0003-00000000000d") },
                    { new Guid("00000000-0000-0000-0002-000000000027"), "BANS_10",        "Утенок-террорист",       10, 3, new Guid("00000000-0000-0000-0003-00000000000d") },
                    { new Guid("00000000-0000-0000-0002-000000000028"), "BANS_30",        "Селезень-Рецидивист",    30, 4, new Guid("00000000-0000-0000-0003-00000000000d") },
                });

            // SEED: timeless award type catalog (6 rows). The specific year
            // and season of a contest live in ContestSeries, grants in UserAward
            // with FKs to both. The catalog does not grow every year — only a
            // new ContestSeries is added for each new contest.
            // Tier for literary contest placements: 1=gold/1st, 2=silver/2nd, 3=bronze/3rd.
            // Special awards (Народное признание, Лучший критик, Угадайка) have Tier=1 (gold).
            migrationBuilder.InsertData(
                table: "AwardTypes",
                columns: new[] { "AwardTypeId", "Code", "Title", "Description", "IconName", "Tier", "SortOrder", "IsActive" },
                values: new object[,]
                {
                    // Descriptions are intentionally generic ("работа" rather than "рассказ") — the catalog
                    // is reused for future contest types (art etc.). The type of a specific
                    // contest surfaces via ContestSeries.ContestType (Literary/Art/...) in the popover.
                    // 1st/2nd/3rd place are the same "Литконкурс" award with a single icon
                    // (cup); the placement is read from the tier color
                    // (1=gold, 2=silver, 3=bronze) on the frontend.
                    { new Guid("00000000-0000-0000-0001-000000000001"), "contest_first",  "Литконкурс",         "Победитель конкурса",                              "trophy-cup",       1, 1, true },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "contest_second", "Литконкурс",         "Серебряный призер конкурса",                       "trophy-cup",       2, 2, true },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "contest_third",  "Литконкурс",         "Бронзовый призер конкурса",                        "trophy-cup",       3, 3, true },
                    // Award title is intentionally "Народное признание, например"
                    // — the ", например" is a deliberate joke in the site's humor
                    // register (cf. "Утенок-террорист", the /about motto). NEVER
                    // "correct" it by dropping the suffix; the full string is the name.
                    { new Guid("00000000-0000-0000-0001-000000000004"), "popular_vote",   "Народное признание, например", "Лучшая работа конкурса по голосованию участников", "ribbon-medal",     1, 4, true },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "best_critic",    "Лучший критик",      "Лучшие рецензии сезона по решению жюри",           "quill-ink",        1, 5, true },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "guesser",        "Угадайка",           "Угадал больше всех авторов конкурсных работ",      "magnifying-glass", 1, 6, true },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "honorary_goblin", "Почетный гоблин",   "Бывший гоблин, отдавший сообществу годы службы",   "goblin",           5, 7, true },
                });

            // SEED: literary contest series. Each contest has
            // a global sequential Number within its ContestType (Literary 1..N,
            // Art 1..M). Year is a display field for the year badge on the tile.
            // We seed Literary 20..23 and Art 1..2 so demo awards show
            // both contest types and a realistic year progression.
            migrationBuilder.InsertData(
                table: "ContestSeries",
                columns: new[] { "ContestSeriesId", "ContestType", "Number", "Year", "TopicUrl", "IsActive" },
                values: new object[,]
                {
                    // ContestType: 0=Literary, 1=Art. TopicUrl is a placeholder for the contest
                    // results (a forum topic); real URLs will be set by the contest committee.
                    { new Guid("00000000-0000-0000-0004-000000000001"), 0, 23, 2024, "https://dm.am/forum/topic/contest-results-lit-23", true },
                    { new Guid("00000000-0000-0000-0004-000000000002"), 0, 22, 2023, "https://dm.am/forum/topic/contest-results-lit-22", true },
                    { new Guid("00000000-0000-0000-0004-000000000003"), 0, 21, 2023, "https://dm.am/forum/topic/contest-results-lit-21", true },
                    { new Guid("00000000-0000-0000-0004-000000000004"), 0, 20, 2022, "https://dm.am/forum/topic/contest-results-lit-20", true },
                    { new Guid("00000000-0000-0000-0004-000000000005"), 1,  2, 2024, "https://dm.am/forum/topic/contest-results-art-2",  true },
                    { new Guid("00000000-0000-0000-0004-000000000006"), 1,  1, 2023, "https://dm.am/forum/topic/contest-results-art-1",  true },
                });

            // SEED: fundraising progress — single-row table with a fixed GUID
            // (zero-family, block 0005). Mirrors HasData in DmDbContext.
            // GET /v1/fundraising reads this row; PUT updates it in place.
            migrationBuilder.InsertData(
                table: "FundraisingGoals",
                columns: new[] { "FundraisingGoalId", "GoalAmount", "CollectedAmount", "ModifiedUtc", "UpdatedByUserId" },
                values: new object[]
                {
                    new Guid("00000000-0000-0000-0005-000000000001"), // FundraisingGoalId (fixed, single row)
                    50000m, // GoalAmount
                    17000m, // CollectedAmount
                    new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), // ModifiedUtc
                    null // UpdatedByUserId (seeded, not updated by anyone yet)
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
                name: "IX_ContestSeries_ContestType_Number",
                table: "ContestSeries",
                columns: new[] { "ContestType", "Number" },
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
                name: "IX_Boards_LastCommentAuthorId",
                table: "Boards",
                column: "LastCommentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_LastCommentId",
                table: "Boards",
                column: "LastCommentId");

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
                name: "IX_Messages_SearchVector",
                table: "Messages",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GlobalChatEventId",
                table: "Messages",
                column: "GlobalChatEventId");

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
                name: "IX_PostReviews_CreatedUtc",
                table: "PostReviews",
                column: "CreatedUtc",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CreatedUtc",
                table: "Posts",
                column: "CreatedUtc",
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CharacterId",
                table: "Posts",
                column: "CharacterId");

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
                name: "IX_Topics_BoardId",
                table: "Topics",
                column: "BoardId");

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
                name: "IX_Uploads_UserId",
                table: "Uploads",
                column: "UserId");

            // Partial indexes on the typed target FKs — speed up batched
            // "find user/character avatars" queries from ImageEnrichment.
            migrationBuilder.CreateIndex(
                name: "IX_Uploads_TargetUserId",
                table: "Uploads",
                column: "TargetUserId",
                filter: "\"TargetUserId\" IS NOT NULL");

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

            // Expression indexes, not column indexes: every username and email
            // lookup in the application compares lower(column) to lower(value),
            // and a b-tree over the raw column cannot serve that predicate — it
            // seq-scans Users on every login. Unique over lower(...) also gives
            // the invariant the product needs: two accounts cannot differ by
            // letter case alone. EF has no model-level expression index, hence
            // raw SQL here (see the note in DmDbContext).
            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_Users_Email_Lower" ON "Users" (lower("Email"));""");
            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_Users_Username_Lower" ON "Users" (lower("Username"));""");

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
                name: "FK_Boards_Comments_LastCommentId",
                table: "Boards",
                column: "LastCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Topics_LastTopicId",
                table: "Boards",
                column: "LastTopicId",
                principalTable: "Topics",
                principalColumn: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Boards_Users_LastCommentAuthorId",
                table: "Boards",
                column: "LastCommentAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

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

            // NOTE: FK_Comments_Topics_EntityId removed — Comment.EntityId is polymorphic
            // (Topic / Game / Blog / Publication). EF Core generates this constraint from
            // `modelBuilder.Entity<Topic>().HasMany(t => t.Comments).WithOne(c => c.Topic)` —
            // the navigation is needed for Include/ProjectTo, but the DB-level FK breaks any
            // comments on non-Topic entities. Referential integrity is held by application
            // logic. When the migration is regenerated this block must be removed again.
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
                name: "FK_Uploads_Users_UserId",
                table: "Uploads",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            // Typed target FKs (the polymorphic EntityId is replaced).
            // SET NULL on target deletion — the Upload record is kept
            // and swept by the GC worker.
            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_TargetUserId",
                table: "Uploads",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Characters_TargetCharacterId",
                table: "Uploads",
                column: "TargetCharacterId",
                principalTable: "Characters",
                principalColumn: "CharacterId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Posts_TargetPostId",
                table: "Uploads",
                column: "TargetPostId",
                principalTable: "Posts",
                principalColumn: "PostId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Users_LastCommentAuthorId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Boards_Users_LastTopicAuthorId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments");

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
                name: "FK_Topics_Users_AuthorId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_DeletedByUserId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_DeletedByUserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_UserId",
                table: "Uploads");

            // Typed target FKs must be dropped before the referenced tables
            // (Posts/Characters/Users) are dropped — Uploads is dropped after them.
            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_TargetUserId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Characters_TargetCharacterId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Posts_TargetPostId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Boards_BoardId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Comments_LastCommentId",
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
                name: "OutboxEvents");

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
                name: "Posts");

            migrationBuilder.DropTable(
                name: "Rubrics");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "AchievementTypes");

            migrationBuilder.DropTable(
                name: "AchievementCategories");

            migrationBuilder.DropTable(
                name: "AwardTypes");

            migrationBuilder.DropTable(
                name: "ContestSeries");

            migrationBuilder.DropTable(
                name: "TagGroups");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Blogs");

            migrationBuilder.DropTable(
                name: "Bans");

            migrationBuilder.DropTable(
                name: "Warnings");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Uploads");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Chats");

            migrationBuilder.DropTable(
                name: "GlobalChatEvents");
        }
    }
}
