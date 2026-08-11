using System;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// The one place a tracked row records who removed it and when.
/// </summary>
/// <remarks>
/// <see cref="ISoftDeletable" /> declares who removed the row and when, and the schema carries
/// both columns for twenty-five tables with a foreign key under the author. Most write paths
/// set the flag alone, so moderation could not answer "who deleted this topic" for anything but
/// blogs — and inside one table, Comments, the answer existed for a blog comment and did not
/// for a forum one, which makes any report over that column wrong rather than incomplete.
/// Declaring a contract the code does not honour is worse than not declaring it.
///
/// Marking through one call keeps the three assignments together: a path that has the author
/// cannot write the flag and forget the audit, because there is nothing to forget.
///
/// SoftDeleteAuditShould is what turns that from a claim into a rule: the two audit columns
/// are assigned in this file and in no other file of the assembly. Rows whose type carries no
/// author and no moment - IRemovable without ISoftDeletable, a warning or a moderator's note -
/// have nothing to record and are outside both the class and the rule.
/// </remarks>
internal static class SoftDelete
{
    /// <summary>
    /// Mark <paramref name="entity" /> deleted by <paramref name="deletedByUserId" />.
    /// </summary>
    /// <remarks>
    /// The author is nullable because some removals have none: a token consumed by the flow
    /// that issued it, a row swept by a background collector. Null there is the honest answer;
    /// null after a person pressed delete is the defect.
    /// </remarks>
    public static void Mark(ISoftDeletable entity, Guid? deletedByUserId, DateTimeOffset deletedUtc)
    {
        entity.IsRemoved = true;
        entity.DeletedByUserId = deletedByUserId;
        entity.DeletedUtc = deletedUtc;
    }
}
