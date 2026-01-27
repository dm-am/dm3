using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace DM.Services.DataAccess.Migrations;

/// <summary>
/// Rename CreateDate to CreatedUtc and LastUpdateDate to ModifiedUtc for terminology unification
/// </summary>
public partial class RenameCreateDateToCreatedUtc : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Comments table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Comments",
            newName: "CreatedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateDate",
            table: "Comments",
            newName: "ModifiedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateUserId",
            table: "Comments",
            newName: "ModifiedByUserId");

        // ForumTopics table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "ForumTopics",
            newName: "CreatedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateDate",
            table: "ForumTopics",
            newName: "ModifiedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateUserId",
            table: "ForumTopics",
            newName: "ModifiedByUserId");

        // Posts table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Posts",
            newName: "CreatedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateDate",
            table: "Posts",
            newName: "ModifiedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateUserId",
            table: "Posts",
            newName: "ModifiedByUserId");

        // Characters table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Characters",
            newName: "CreatedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateDate",
            table: "Characters",
            newName: "ModifiedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateUserId",
            table: "Characters",
            newName: "ModifiedByUserId");

        // Games table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Games",
            newName: "CreatedUtc");


        // Messages table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Messages",
            newName: "CreatedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateDate",
            table: "Messages",
            newName: "ModifiedUtc");

        migrationBuilder.RenameColumn(
            name: "LastUpdateUserId",
            table: "Messages",
            newName: "ModifiedByUserId");


        // Users table
        migrationBuilder.RenameColumn(
            name: "RegistrationDate",
            table: "Users",
            newName: "CreatedUtc");

        migrationBuilder.RenameColumn(
            name: "LastVisitDate",
            table: "Users",
            newName: "LastActivityUtc");

        // Uploads table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Uploads",
            newName: "CreatedUtc");

        // Reviews table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Reviews",
            newName: "CreatedUtc");

        // Tokens table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Tokens",
            newName: "CreatedUtc");

        // Bans table
        migrationBuilder.RenameColumn(
            name: "StartDate",
            table: "Bans",
            newName: "StartedUtc");

        migrationBuilder.RenameColumn(
            name: "EndDate",
            table: "Bans",
            newName: "EndedUtc");

        // Warnings table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "Warnings",
            newName: "CreatedUtc");

        // Boards table (LastCommentDate denormalized field)
        migrationBuilder.RenameColumn(
            name: "LastCommentDate",
            table: "Boards",
            newName: "LastCommentUtc");

        // PendingPosts table
        migrationBuilder.RenameColumn(
            name: "CreateDate",
            table: "PendingPosts",
            newName: "CreatedUtc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Comments table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Comments",
            newName: "CreateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedUtc",
            table: "Comments",
            newName: "LastUpdateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedByUserId",
            table: "Comments",
            newName: "LastUpdateUserId");

        // ForumTopics table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "ForumTopics",
            newName: "CreateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedUtc",
            table: "ForumTopics",
            newName: "LastUpdateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedByUserId",
            table: "ForumTopics",
            newName: "LastUpdateUserId");

        // Posts table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Posts",
            newName: "CreateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedUtc",
            table: "Posts",
            newName: "LastUpdateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedByUserId",
            table: "Posts",
            newName: "LastUpdateUserId");

        // Characters table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Characters",
            newName: "CreateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedUtc",
            table: "Characters",
            newName: "LastUpdateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedByUserId",
            table: "Characters",
            newName: "LastUpdateUserId");

        // Games table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Games",
            newName: "CreateDate");


        // Messages table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Messages",
            newName: "CreateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedUtc",
            table: "Messages",
            newName: "LastUpdateDate");

        migrationBuilder.RenameColumn(
            name: "ModifiedByUserId",
            table: "Messages",
            newName: "LastUpdateUserId");


        // Users table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Users",
            newName: "RegistrationDate");

        migrationBuilder.RenameColumn(
            name: "LastActivityUtc",
            table: "Users",
            newName: "LastVisitDate");

        // Uploads table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Uploads",
            newName: "CreateDate");

        // Reviews table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Reviews",
            newName: "CreateDate");

        // Tokens table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Tokens",
            newName: "CreateDate");

        // Bans table
        migrationBuilder.RenameColumn(
            name: "StartedUtc",
            table: "Bans",
            newName: "StartDate");

        migrationBuilder.RenameColumn(
            name: "EndedUtc",
            table: "Bans",
            newName: "EndDate");

        // Warnings table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "Warnings",
            newName: "CreateDate");

        // Boards table
        migrationBuilder.RenameColumn(
            name: "LastCommentUtc",
            table: "Boards",
            newName: "LastCommentDate");

        // PendingPosts table
        migrationBuilder.RenameColumn(
            name: "CreatedUtc",
            table: "PendingPosts",
            newName: "CreateDate");
    }
}
