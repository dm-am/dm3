using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Destroys the objects of uploads whose grace period has run out, and the rows
/// that named them.
/// </summary>
/// <remarks>
/// Uploads are the one area with no owning module: every contract they need is
/// declared here and answered by the persistence layer. The grace period itself
/// is a product rule and lives with the others in
/// <see cref="Configuration.UploadPolicy" />.
/// </remarks>
public interface IUploadOrphanCollector
{
    /// <summary>
    /// Runs one sweep.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the sweep found and removed.</returns>
    Task<UploadSweepResult> SweepAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// What one sweep of orphaned uploads did.
/// </summary>
/// <param name="Candidates">Rows past the grace period this pass looked at.</param>
/// <param name="ObjectsRemoved">Objects the store confirmed are gone.</param>
/// <param name="RecordsDeleted">Rows dropped, which is the ones whose objects all went.</param>
public record UploadSweepResult(int Candidates, int ObjectsRemoved, int RecordsDeleted);
