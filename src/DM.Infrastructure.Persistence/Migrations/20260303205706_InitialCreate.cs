using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    Title = table.Column<string>(type: "text", nullable: false)
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
                    TagGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false)
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
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PremoderationStatus = table.Column<int>(type: "integer", nullable: false),
                    MentorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftVisibility = table.Column<int>(type: "integer", nullable: false),
                    CommentsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PublicationCount = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
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
                    Description = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ViewPolicy = table.Column<int>(type: "integer", nullable: false),
                    CreateTopicPolicy = table.Column<int>(type: "integer", nullable: false),
                    TopicsCount = table.Column<int>(type: "integer", nullable: false),
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Race = table.Column<string>(type: "text", nullable: true),
                    Class = table.Column<string>(type: "text", nullable: true),
                    Alignment = table.Column<int>(type: "integer", nullable: true),
                    Appearance = table.Column<string>(type: "text", nullable: true),
                    Temper = table.Column<string>(type: "text", nullable: true),
                    Story = table.Column<string>(type: "text", nullable: true),
                    Skills = table.Column<string>(type: "text", nullable: true),
                    Inventory = table.Column<string>(type: "text", nullable: true),
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
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    LastMessageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.ChatId);
                });

            migrationBuilder.CreateTable(
                name: "CommentEditHistory",
                columns: table => new
                {
                    CommentEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentEditHistory", x => x.CommentEditId);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "Games",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleaseDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PremoderationStatus = table.Column<int>(type: "integer", nullable: false),
                    IsFinished = table.Column<bool>(type: "boolean", nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecruitmentOpen = table.Column<bool>(type: "boolean", nullable: false),
                    RecruitmentPlayerLimit = table.Column<int>(type: "integer", nullable: true),
                    RecruitmentStartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributeSchemaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SystemName = table.Column<string>(type: "text", nullable: true),
                    NarrativeSetting = table.Column<string>(type: "text", nullable: true),
                    Info = table.Column<string>(type: "text", nullable: true),
                    HideTemper = table.Column<bool>(type: "boolean", nullable: false),
                    HideSkills = table.Column<bool>(type: "boolean", nullable: false),
                    HideInventory = table.Column<bool>(type: "boolean", nullable: false),
                    HideStory = table.Column<bool>(type: "boolean", nullable: false),
                    DisableAlignment = table.Column<bool>(type: "boolean", nullable: false),
                    HideDiceResult = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPrivateMessages = table.Column<bool>(type: "boolean", nullable: false),
                    HidePostStats = table.Column<bool>(type: "boolean", nullable: false),
                    CommentsAccessMode = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AccessType = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OrderNumber = table.Column<double>(type: "double precision", nullable: false),
                    ViewPrivateText = table.Column<bool>(type: "boolean", nullable: false),
                    ViewDiceResults = table.Column<bool>(type: "boolean", nullable: false),
                    DiceEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextRoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "GlobalChatEventParticipants",
                columns: table => new
                {
                    GlobalChatEventParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOrganizer = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GlobalChatEventId = table.Column<Guid>(type: "uuid", nullable: true)
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
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_PostPendencies_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
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
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    MasterMessage = table.Column<string>(type: "text", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.PostId);
                    table.ForeignKey(
                        name: "FK_Posts_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                    table.ForeignKey(
                        name: "FK_Posts_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Publications",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlogId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "Reviews",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    SignValue = table.Column<short>(type: "smallint", nullable: true),
                    ReasonType = table.Column<int>(type: "integer", nullable: true),
                    PostAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_Reviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "GameId");
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
                    table.ForeignKey(
                        name: "FK_RoomAccesses_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                    table.ForeignKey(
                        name: "FK_RoomAccesses_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
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
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_Tokens_Blogs_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Blogs",
                        principalColumn: "BlogId");
                    table.ForeignKey(
                        name: "FK_Tokens_Games_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Games",
                        principalColumn: "GameId");
                });

            migrationBuilder.CreateTable(
                name: "TopicEdits",
                columns: table => new
                {
                    TopicEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsAttached = table.Column<bool>(type: "boolean", nullable: false),
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
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Original = table.Column<bool>(type: "boolean", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MediumFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SmallFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.UploadId);
                    table.ForeignKey(
                        name: "FK_Uploads_Characters_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId");
                    table.ForeignKey(
                        name: "FK_Uploads_Games_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Games",
                        principalColumn: "GameId");
                    table.ForeignKey(
                        name: "FK_Uploads_Posts_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Posts",
                        principalColumn: "PostId");
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
                    IsHonorary = table.Column<bool>(type: "boolean", nullable: false),
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
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false)
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
                    ApprovalTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                name: "IX_Characters_ModifiedByUserId",
                table: "Characters",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_LastMessageId",
                table: "Chats",
                column: "LastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentEditHistory_CommentId",
                table: "CommentEditHistory",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentEditHistory_EditorUserId",
                table: "CommentEditHistory",
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
                name: "IX_Comments_ModifiedByUserId",
                table: "Comments",
                column: "ModifiedByUserId");

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
                name: "IX_Games_AuthorId",
                table: "Games",
                column: "AuthorId");

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
                name: "IX_Messages_ChatId",
                table: "Messages",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GlobalChatEventId",
                table: "Messages",
                column: "GlobalChatEventId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ModifiedByUserId",
                table: "Messages",
                column: "ModifiedByUserId");

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
                name: "IX_NotepadEntries_AuthorId",
                table: "NotepadEntries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotepadEntries_CategoryId",
                table: "NotepadEntries",
                column: "CategoryId");

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
                name: "IX_Posts_AuthorId",
                table: "Posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CharacterId",
                table: "Posts",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_DeletedByUserId",
                table: "Posts",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_ModifiedByUserId",
                table: "Posts",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_RoomId",
                table: "Posts",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_AuthorId",
                table: "Publications",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_BlogId",
                table: "Publications",
                column: "BlogId");

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
                name: "IX_Reviews_GameId",
                table: "Reviews",
                column: "GameId",
                filter: "\"IsRemoved\" = false AND \"GameId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ModifiedByUserId",
                table: "Reviews",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PostAuthorId",
                table: "Reviews",
                column: "PostAuthorId",
                filter: "\"IsRemoved\" = false AND \"PostAuthorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TargetType_TargetId",
                table: "Reviews",
                columns: new[] { "TargetType", "TargetId" },
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_TargetType_TargetId",
                table: "Reviews",
                columns: new[] { "UserId", "TargetType", "TargetId" },
                unique: true,
                filter: "\"IsRemoved\" = false");

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
                name: "IX_Topics_ModifiedByUserId",
                table: "Topics",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_EntityId",
                table: "Uploads",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UserId",
                table: "Uploads",
                column: "UserId");

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
                name: "IX_UserChatLinks_UserId",
                table: "UserChatLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_UserId",
                table: "UserContacts",
                column: "UserId");

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
                name: "IX_Users_Email_Lower",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username_Lower",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_AuthorId",
                table: "Warnings",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_TargetUserId",
                table: "Warnings",
                column: "TargetUserId");

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
                name: "FK_Boards_Users_LastCommentAuthorId",
                table: "Boards",
                column: "LastCommentAuthorId",
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
                name: "FK_Characters_Users_ModifiedByUserId",
                table: "Characters",
                column: "ModifiedByUserId",
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
                name: "FK_CommentEditHistory_Comments_CommentId",
                table: "CommentEditHistory",
                column: "CommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CommentEditHistory_Users_EditorUserId",
                table: "CommentEditHistory",
                column: "EditorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Topics_EntityId",
                table: "Comments",
                column: "EntityId",
                principalTable: "Topics",
                principalColumn: "TopicId");

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
                name: "FK_Comments_Users_ModifiedByUserId",
                table: "Comments",
                column: "ModifiedByUserId",
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
                name: "FK_Games_Users_AuthorId",
                table: "Games",
                column: "AuthorId",
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
                name: "FK_Messages_Users_ModifiedByUserId",
                table: "Messages",
                column: "ModifiedByUserId",
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
                name: "FK_NotepadEntries_Users_AuthorId",
                table: "NotepadEntries",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Posts_Users_ModifiedByUserId",
                table: "Posts",
                column: "ModifiedByUserId",
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
                name: "FK_Reviews_Users_ModifiedByUserId",
                table: "Reviews",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_PostAuthorId",
                table: "Reviews",
                column: "PostAuthorId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_UserId",
                table: "Reviews",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomAccesses_Users_ReaderUserId",
                table: "RoomAccesses",
                column: "ReaderUserId",
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
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_UserId",
                table: "Tickets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

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
                principalColumn: "UserId");

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
                name: "FK_Topics_Users_ModifiedByUserId",
                table: "Topics",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_Users_EntityId",
                table: "Uploads",
                column: "EntityId",
                principalTable: "Users",
                principalColumn: "UserId");

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
                name: "FK_Boards_Users_LastCommentAuthorId",
                table: "Boards");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_AuthorId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_DeletedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_ModifiedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_ModifiedByUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_AuthorId",
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
                name: "FK_Messages_Users_ModifiedByUserId",
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
                name: "FK_Posts_Users_ModifiedByUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_AuthorId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_DeletedByUserId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Users_ModifiedByUserId",
                table: "Topics");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_EntityId",
                table: "Uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_Uploads_Users_UserId",
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
                name: "CommentEditHistory");

            migrationBuilder.DropTable(
                name: "GameAssistants");

            migrationBuilder.DropTable(
                name: "GameBlacklists");

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
                name: "PostEdits");

            migrationBuilder.DropTable(
                name: "PostPendencies");

            migrationBuilder.DropTable(
                name: "Publications");

            migrationBuilder.DropTable(
                name: "Reviews");

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
                name: "UserBlacklists");

            migrationBuilder.DropTable(
                name: "UserChatLinks");

            migrationBuilder.DropTable(
                name: "UserContacts");

            migrationBuilder.DropTable(
                name: "UserLoginRecords");

            migrationBuilder.DropTable(
                name: "UsernameChangeRequests");

            migrationBuilder.DropTable(
                name: "UsernameHistories");

            migrationBuilder.DropTable(
                name: "UserProfileNotes");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "NotepadCategories");

            migrationBuilder.DropTable(
                name: "Rubrics");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "TagGroups");

            migrationBuilder.DropTable(
                name: "Blogs");

            migrationBuilder.DropTable(
                name: "Bans");

            migrationBuilder.DropTable(
                name: "Warnings");

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
