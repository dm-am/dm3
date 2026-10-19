using System.Net;
using System.Text.Json;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// The warning endpoints over rows written straight into the tables: warnings,
/// avatars, and the reads of the answers.
/// </summary>
public abstract class WarningControllerTestBase : IntegrationTestBase
{
    protected WarningControllerTestBase(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Identifiers that order the same way in PostgreSQL and in .NET.
    /// </summary>
    /// <remarks>
    /// The database compares a uuid byte by byte and Guid.CompareTo reads the first eight bytes as numbers; rows
    /// differing in the last byte order alike.
    /// </remarks>
    protected static Guid LogId(int lastByte) =>
        Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb{lastByte:x2}");

    /// <summary>One warning, written straight into the table.</summary>
    /// <remarks>The create endpoint needs a moderator and raises an event; the log is read anonymously.</remarks>
    protected static DbWarning Row(
        Guid warningId,
        DateTimeOffset createdUtc,
        Guid? targetUserId = null,
        string text = "Причина предупреждения",
        int points = 1,
        int? originalPoints = null,
        string? snapshot = null,
        Guid? authorId = null,
        Guid? entityId = null,
        WarningEntityType entityType = WarningEntityType.Unknown,
        Guid? pointsChangedByUserId = null,
        DateTimeOffset? pointsChangedUtc = null,
        string? pointsChangeText = null)
        => new()
        {
            WarningId = warningId,
            TargetUserId = targetUserId ?? TestConstants.SecondUserId,
            AuthorId = authorId ?? TestConstants.ModeratorUserId,
            EntityId = entityId ?? Guid.Empty,
            EntityType = entityType,
            CreatedUtc = createdUtc,
            Text = text,
            Points = points,
            OriginalPoints = originalPoints ?? points,
            EntitySnapshot = snapshot,
            PointsChangedByUserId = pointsChangedByUserId,
            PointsChangedUtc = pointsChangedUtc,
            PointsChangeText = pointsChangeText
        };

    protected async Task Seed(params DbWarning[] rows)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Warnings.AddRange(rows);
        await db.SaveChangesAsync();
    }

    protected async Task Unseed(params Guid[] ids)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Warnings.Where(w => ids.Contains(w.WarningId)).ExecuteDeleteAsync();
    }

    protected static async Task<JsonDocument> ReadDocument(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return JsonDocument.Parse(body);
    }

    protected static List<Guid> IdsOf(JsonDocument document) => document.RootElement
        .GetProperty("resources").EnumerateArray()
        .Select(row => row.GetProperty("id").GetGuid())
        .ToList();

    /// <summary>
    /// An avatar of a user, written straight into the table: replaced by its
    /// owner (deleted with no author) unless the test says otherwise.
    /// </summary>
    protected static DbUpload Avatar(Guid uploadId, Guid ownerId, bool removed = true, Guid? deletedBy = null) => new()
    {
        UploadId = uploadId,
        CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30),
        ConfirmedUtc = DateTimeOffset.UtcNow.AddDays(-30),
        UserId = ownerId,
        TargetUserId = ownerId,
        Type = UploadType.UserAvatar,
        Status = UploadStatus.Confirmed,
        Original = true,
        ContentType = "image/png",
        SizeBytes = 2048,
        Width = 731,
        Height = 793,
        ObjectKey = $"avatars/{ownerId:N}_{uploadId:N}.png",
        FilePath = $"https://cdn.example.test/dm/avatars/{ownerId:N}_{uploadId:N}.png",
        IsRemoved = removed,
        DeletedByUserId = removed ? deletedBy : null,
        DeletedUtc = removed ? DateTimeOffset.UtcNow.AddDays(-10) : null
    };

    /// <summary>A warning on the profile of the user, the way the server writes one.</summary>
    protected static DbWarning OnProfile(
        Guid warningId, Guid targetUserId, string? status, string? info, Guid? avatarUploadId,
        string text = "Неприемлемый аватар")
    {
        var row = Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), targetUserId: targetUserId, text: text,
            points: 3, entityId: targetUserId, entityType: WarningEntityType.Profile, snapshot: info);
        row.ProfileStatusSnapshot = status;
        row.ProfileAvatarUploadId = avatarUploadId;
        return row;
    }

    protected async Task SeedUpload(DbUpload upload)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Uploads.Add(upload);
        await db.SaveChangesAsync();
    }

    protected async Task UnseedUpload(Guid uploadId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Uploads.IgnoreQueryFilters().Where(u => u.UploadId == uploadId).ExecuteDeleteAsync();
    }

    protected static JsonElement RowOf(JsonDocument document, Guid warningId) => document.RootElement
        .GetProperty("resources").EnumerateArray()
        .Single(item => item.GetProperty("id").GetGuid() == warningId);
}
