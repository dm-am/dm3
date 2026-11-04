using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

/// <summary>
/// The partial index over live rows: a soft-deleted row leaves room for its replacement.
/// </summary>
internal static class LiveRowsFilter
{
    internal static IndexBuilder<TEntity> HasLiveRowsFilter<TEntity>(this IndexBuilder<TEntity> index) =>
        index.HasFilter("\"IsRemoved\" = false");
}
