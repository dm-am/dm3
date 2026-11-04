using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Infrastructure.Persistence.Shared.Attachments;

/// <summary>
/// The upload rows that are a post's attachments: read for a page of posts at
/// once, and retired together with whatever removal took their carrier away.
/// </summary>
/// <remarks>
/// An attachment knows its carrier by one column, TargetPostId, and by nothing
/// else. Three removals take attachments with them - a post, a room and a game -
/// and each used to write the same retirement out by hand.
/// </remarks>
internal static class AttachmentRows
{
    /// <summary>
    /// The files attached to each of <paramref name="postIds" />, in the order
    /// they were added. A post with none is absent from the answer.
    /// </summary>
    /// <remarks>
    /// One IN-query for the page, for the same reason
    /// PostRepository.EnrichWithCharacterPictures is one: a post may carry
    /// several attachments, so an inline collection projection would be a
    /// correlated subquery per row, and a room lists twenty rows at a time.
    ///
    /// The object key is not selected. It is the address of the bytes in a bucket
    /// whose post prefix answers nobody anonymously, and the only reason to have
    /// it here would be to put it somewhere a reader can see.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<PostAttachment>>> OfPostsAsync(
        DmDbContext dbContext, IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken = default)
    {
        if (postIds.Count == 0) return new Dictionary<Guid, IReadOnlyCollection<PostAttachment>>();

        var rows = await dbContext.Uploads
            .Where(u => u.TargetPostId != null
                && postIds.Contains(u.TargetPostId.Value)
                && u.Type == UploadType.PostAttachment)
            .OrderBy(u => u.CreatedUtc)
            .ThenBy(u => u.UploadId)
            .Select(u => new
            {
                PostId = u.TargetPostId!.Value,
                Attachment = new PostAttachment
                {
                    Id = u.UploadId,
                    FileName = u.FileName ?? string.Empty,
                    ContentType = u.ContentType ?? string.Empty,
                    SizeBytes = u.SizeBytes,
                    Width = u.Width,
                    Height = u.Height,
                    CreatedUtc = u.CreatedUtc,
                    AddresseesJson = u.AddresseesJson,
                },
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.PostId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<PostAttachment>)
                g.Select(x => x.Attachment).ToList());
    }

    /// <summary>
    /// Soft-delete <paramref name="attachments" /> on behalf of
    /// <paramref name="deletedByUserId" />: the flag and both audit columns in one
    /// statement.
    /// </summary>
    /// <remarks>
    /// Which rows is the caller's to say, because each carrier reaches its files
    /// differently: a post by the column itself, a room and a game through the
    /// posts the column points at.
    /// </remarks>
    public static Task<int> RetireAsync(
        IQueryable<DbUpload> attachments, Guid? deletedByUserId, DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        SoftDelete.MarkWhere(attachments, deletedByUserId, now, cancellationToken);
}
