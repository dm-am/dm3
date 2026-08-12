using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <inheritdoc />
internal class UploadGarbageCollector : IUploadGarbageCollector
{
    private readonly DmDbContext _db;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public UploadGarbageCollector(
        DmDbContext db,
        IDateTimeProvider dateTimeProvider)
    {
        _db = db;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task CollectObsoleteAsync(Guid entityId, UploadType type)
    {
        // A post carries as many attachments as its author put there, so "the
        // newest one wins" would delete all but the last of them. The caller
        // names the slot it replaced and a multi-slot type is refused here,
        // rather than at the first attachment flow that reaches this method.
        if (type is not (UploadType.UserAvatar or UploadType.CharacterAvatar))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type,
                "Only single-slot upload types have obsolete predecessors");
        }

        // Both avatar types keep one live upload per target, and the type is what
        // separates them: an identifier belongs either to a user or to a
        // character, and matching on the type as well keeps a row of the other
        // kind out even if the identifiers ever collide.
        var uploads = await _db.Uploads
            .Where(u => !u.IsRemoved
                && u.Type == type
                && (u.TargetUserId == entityId || u.TargetCharacterId == entityId))
            .OrderByDescending(u => u.CreatedUtc)
            .ToListAsync();

        if (uploads.Count <= 1)
        {
            return;
        }

        foreach (var obsolete in uploads.Skip(1))
        {
            // No author: a sweep is not somebody pressing delete, which is the case the
            // nullable author of Mark exists for. The moment starts the sweeper's grace
            // period, so the value comes from the same clock the sweeper compares it
            // against. The object itself stays in the bucket until that period is over:
            // destroying it here would leave a row that still says it is restorable
            // pointing at nothing.
            SoftDelete.Mark(obsolete, null, _dateTimeProvider.Now);
        }

        await _db.SaveChangesAsync();
    }
}
