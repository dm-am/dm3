using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DM.Services.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class _20260127000000_AddOpenIddict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumTopics_Fora_ForumId",
                table: "ForumTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_LastUpdateUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Warnings_ChatMessages_EntityId",
                table: "Warnings");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ForumModerators");

            migrationBuilder.DropTable(
                name: "Fora");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Warnings",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "RegistrationDate",
                table: "Users",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "LastVisitDate",
                table: "Users",
                newName: "LastActivityUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Uploads",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Tokens",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Reviews",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "LastUpdateUserId",
                table: "Posts",
                newName: "ModifiedByUserId");

            migrationBuilder.RenameColumn(
                name: "LastUpdateDate",
                table: "Posts",
                newName: "ModifiedUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Posts",
                newName: "CreatedUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Posts_LastUpdateUserId",
                table: "Posts",
                newName: "IX_Posts_ModifiedByUserId");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "PendingPosts",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "LastUpdateDate",
                table: "Messages",
                newName: "ModifiedUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Messages",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "SettingName",
                table: "Games",
                newName: "NarrativeSetting");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Games",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "LastUpdateDate",
                table: "ForumTopics",
                newName: "ModifiedUtc");

            migrationBuilder.RenameColumn(
                name: "ForumId",
                table: "ForumTopics",
                newName: "BoardId");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "ForumTopics",
                newName: "CreatedUtc");

            migrationBuilder.RenameIndex(
                name: "IX_ForumTopics_ForumId",
                table: "ForumTopics",
                newName: "IX_ForumTopics_BoardId");

            migrationBuilder.RenameColumn(
                name: "LastUpdateDate",
                table: "Comments",
                newName: "ModifiedUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Comments",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "LastUpdateDate",
                table: "Characters",
                newName: "ModifiedUtc");

            migrationBuilder.RenameColumn(
                name: "CreateDate",
                table: "Characters",
                newName: "CreatedUtc");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "Bans",
                newName: "StartedUtc");

            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "Bans",
                newName: "EndedUtc");

            migrationBuilder.AddColumn<int>(
                name: "PasswordHashVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DiceEnabled",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ViewDiceResults",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ViewPrivateText",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedUtc",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinished",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecruitmentOpen",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PremoderationStatus",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecruitmentPlayerLimit",
                table: "Games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecruitmentStartedUtc",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "ForumTopics",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "ForumTopics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "ForumTopics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "Comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDead",
                table: "Characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlayerExiled",
                table: "Characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlayerLeft",
                table: "Characters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "Characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_Boards_Comments_LastCommentId",
                        column: x => x.LastCommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId");
                    table.ForeignKey(
                        name: "FK_Boards_Users_LastCommentAuthorId",
                        column: x => x.LastCommentAuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
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
                    table.ForeignKey(
                        name: "FK_CharacterEdits_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "CharacterId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterEdits_Users_EditorUserId",
                        column: x => x.EditorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommentEdits",
                columns: table => new
                {
                    CommentEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentEdits", x => x.CommentEditId);
                    table.ForeignKey(
                        name: "FK_CommentEdits_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommentEdits_Users_EditorUserId",
                        column: x => x.EditorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_MessageEdits_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageEdits_Users_EditorUserId",
                        column: x => x.EditorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
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
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.Id);
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
                    table.ForeignKey(
                        name: "FK_PostEdits_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "PostId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostEdits_Users_EditorUserId",
                        column: x => x.EditorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicEdits",
                columns: table => new
                {
                    TopicEditId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForumTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicEdits", x => x.TopicEditId);
                    table.ForeignKey(
                        name: "FK_TopicEdits_ForumTopics_ForumTopicId",
                        column: x => x.ForumTopicId,
                        principalTable: "ForumTopics",
                        principalColumn: "ForumTopicId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicEdits_Users_EditorUserId",
                        column: x => x.EditorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_BoardModerators_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardModerators_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    AuthorizationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId",
                unique: true,
                filter: "\"IsRemoved\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_DeletedByUserId",
                table: "Posts",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedByUserId",
                table: "Messages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ModifiedByUserId",
                table: "Messages",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumTopics_DeletedByUserId",
                table: "ForumTopics",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumTopics_ModifiedByUserId",
                table: "ForumTopics",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_DeletedByUserId",
                table: "Comments",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ModifiedByUserId",
                table: "Comments",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_DeletedByUserId",
                table: "Characters",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ModifiedByUserId",
                table: "Characters",
                column: "ModifiedByUserId");

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
                name: "IX_CharacterEdits_CharacterId",
                table: "CharacterEdits",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEdits_EditorUserId",
                table: "CharacterEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentEdits_CommentId",
                table: "CommentEdits",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentEdits_EditorUserId",
                table: "CommentEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEdits_EditorUserId",
                table: "MessageEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEdits_MessageId",
                table: "MessageEdits",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                table: "OpenIddictTokens",
                column: "ReferenceId",
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
                name: "IX_TopicEdits_EditorUserId",
                table: "TopicEdits",
                column: "EditorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicEdits_ForumTopicId",
                table: "TopicEdits",
                column: "ForumTopicId");

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
                name: "FK_ForumTopics_Boards_BoardId",
                table: "ForumTopics",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "BoardId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumTopics_Users_DeletedByUserId",
                table: "ForumTopics",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumTopics_Users_ModifiedByUserId",
                table: "ForumTopics",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Likes_Messages_EntityId",
                table: "Likes",
                column: "EntityId",
                principalTable: "Messages",
                principalColumn: "MessageId",
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
                name: "FK_Warnings_Messages_EntityId",
                table: "Warnings",
                column: "EntityId",
                principalTable: "Messages",
                principalColumn: "MessageId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_DeletedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_ModifiedByUserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_DeletedByUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Users_ModifiedByUserId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumTopics_Boards_BoardId",
                table: "ForumTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumTopics_Users_DeletedByUserId",
                table: "ForumTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumTopics_Users_ModifiedByUserId",
                table: "ForumTopics");

            migrationBuilder.DropForeignKey(
                name: "FK_Likes_Messages_EntityId",
                table: "Likes");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_ModifiedByUserId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_DeletedByUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_ModifiedByUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Warnings_Messages_EntityId",
                table: "Warnings");

            migrationBuilder.DropTable(
                name: "BoardModerators");

            migrationBuilder.DropTable(
                name: "CharacterEdits");

            migrationBuilder.DropTable(
                name: "CommentEdits");

            migrationBuilder.DropTable(
                name: "MessageEdits");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "OutboxEvents");

            migrationBuilder.DropTable(
                name: "PostEdits");

            migrationBuilder.DropTable(
                name: "TopicEdits");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Posts_DeletedByUserId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Messages_DeletedByUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ModifiedByUserId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_ForumTopics_DeletedByUserId",
                table: "ForumTopics");

            migrationBuilder.DropIndex(
                name: "IX_ForumTopics_ModifiedByUserId",
                table: "ForumTopics");

            migrationBuilder.DropIndex(
                name: "IX_Comments_DeletedByUserId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ModifiedByUserId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Characters_DeletedByUserId",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_ModifiedByUserId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "PasswordHashVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DiceEnabled",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ViewDiceResults",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "ViewPrivateText",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ClosedUtc",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsFinished",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsRecruitmentOpen",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "PremoderationStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RecruitmentPlayerLimit",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RecruitmentStartedUtc",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "ForumTopics");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ForumTopics");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "ForumTopics");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "IsDead",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "IsPlayerExiled",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "IsPlayerLeft",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Warnings",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "LastActivityUtc",
                table: "Users",
                newName: "LastVisitDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Users",
                newName: "RegistrationDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Uploads",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Tokens",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Reviews",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "ModifiedUtc",
                table: "Posts",
                newName: "LastUpdateDate");

            migrationBuilder.RenameColumn(
                name: "ModifiedByUserId",
                table: "Posts",
                newName: "LastUpdateUserId");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Posts",
                newName: "CreateDate");

            migrationBuilder.RenameIndex(
                name: "IX_Posts_ModifiedByUserId",
                table: "Posts",
                newName: "IX_Posts_LastUpdateUserId");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "PendingPosts",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "ModifiedUtc",
                table: "Messages",
                newName: "LastUpdateDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Messages",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "NarrativeSetting",
                table: "Games",
                newName: "SettingName");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Games",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "ModifiedUtc",
                table: "ForumTopics",
                newName: "LastUpdateDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "ForumTopics",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "BoardId",
                table: "ForumTopics",
                newName: "ForumId");

            migrationBuilder.RenameIndex(
                name: "IX_ForumTopics_BoardId",
                table: "ForumTopics",
                newName: "IX_ForumTopics_ForumId");

            migrationBuilder.RenameColumn(
                name: "ModifiedUtc",
                table: "Comments",
                newName: "LastUpdateDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Comments",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "ModifiedUtc",
                table: "Characters",
                newName: "LastUpdateDate");

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                table: "Characters",
                newName: "CreateDate");

            migrationBuilder.RenameColumn(
                name: "StartedUtc",
                table: "Bans",
                newName: "StartDate");

            migrationBuilder.RenameColumn(
                name: "EndedUtc",
                table: "Bans",
                newName: "EndDate");

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    ChatMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.ChatMessageId);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fora",
                columns: table => new
                {
                    ForumId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastCommentAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommentsCount = table.Column<int>(type: "integer", nullable: false),
                    CreateTopicPolicy = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LastCommentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCommentTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    TopicsCount = table.Column<int>(type: "integer", nullable: false),
                    ViewPolicy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fora", x => x.ForumId);
                    table.ForeignKey(
                        name: "FK_Fora_Comments_LastCommentId",
                        column: x => x.LastCommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Fora_Users_LastCommentAuthorId",
                        column: x => x.LastCommentAuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ForumModerators",
                columns: table => new
                {
                    ForumModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForumId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumModerators", x => x.ForumModeratorId);
                    table.ForeignKey(
                        name: "FK_ForumModerators_Fora_ForumId",
                        column: x => x.ForumId,
                        principalTable: "Fora",
                        principalColumn: "ForumId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ForumModerators_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Fora_LastCommentAuthorId",
                table: "Fora",
                column: "LastCommentAuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Fora_LastCommentId",
                table: "Fora",
                column: "LastCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumModerators_ForumId",
                table: "ForumModerators",
                column: "ForumId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumModerators_UserId",
                table: "ForumModerators",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumTopics_Fora_ForumId",
                table: "ForumTopics",
                column: "ForumId",
                principalTable: "Fora",
                principalColumn: "ForumId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Users_LastUpdateUserId",
                table: "Posts",
                column: "LastUpdateUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warnings_ChatMessages_EntityId",
                table: "Warnings",
                column: "EntityId",
                principalTable: "ChatMessages",
                principalColumn: "ChatMessageId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
