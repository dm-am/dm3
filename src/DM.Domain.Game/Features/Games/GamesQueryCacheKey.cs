using System;
using System.Collections.Generic;
using System.Linq;

namespace DM.Domain.Game.Features.Games;

internal static class GamesQueryCacheKey
{
    private const string GamesByStatusCacheKeyPrefix = "GamesByStatus_";

    /// <remarks>
    /// What may be cached is exactly what the key below tells apart; a test walks the query's properties.
    /// </remarks>
    internal static bool IsCacheableAnonymousQuery(GamesQuery query) =>
        query.Skip == 0
        && string.IsNullOrEmpty(query.Search)
        && query.RequiredTags == null
        && query.OptionalTags == null
        && query.ExcludedTags == null
        && (query.OwnerUsernames == null || query.OwnerUsernames.Count == 0)
        && string.IsNullOrEmpty(query.PlayerUsername)
        && query.PlayerParticipation == null
        // Absent rather than "not true", so the key tells apart everything this serves.
        && query.Participating is null
        && query.PremoderationStatuses is null or { Count: 0 }
        // A listing cleaned for somebody must never become the entry every guest is served.
        && query.ExcludedMasterIds is null or { Count: 0 }
        && !query.CreatedFromUtc.HasValue && !query.CreatedToUtc.HasValue
        && !query.ActivatedFromUtc.HasValue && !query.ActivatedToUtc.HasValue
        && !query.ClosedFromUtc.HasValue && !query.ClosedToUtc.HasValue
        && !query.RecruitmentStartedFromUtc.HasValue && !query.RecruitmentStartedToUtc.HasValue;

    /// <remarks>
    /// Normalised the way the repository reads the fields, so two listings never share an entry.
    /// </remarks>
    internal static string BuildAnonymousCacheKey(GamesQuery query)
    {
        var statusPart = query.Statuses is { Count: > 0 }
            ? string.Join("_", query.Statuses.OrderBy(status => status))
            : "all";
        var recruitingPart = query.RecruitmentFilter.HasValue
            ? $"_recruiting_{query.RecruitmentFilter.Value}"
            : "";
        var closedReasonPart = query.ClosedReasonFilter.HasValue
            ? $"_closedReason_{query.ClosedReasonFilter.Value}"
            : "";

        // Exactly what ApplySorting reads.
        var order = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";
        var sortPart = $"_sort_{query.SortBy?.ToLowerInvariant() ?? ""}_{order}";

        return $"{GamesByStatusCacheKeyPrefix}{statusPart}{recruitingPart}{closedReasonPart}" +
            $"{sortPart}_take_{query.Take}";
    }

    internal static string GetQueryKey(GamesQuery query)
    {
        var parts = new List<string>
        {
            query.Skip.ToString(),
            query.Take.ToString(),
            query.Search ?? "",
            query.SortBy ?? "",
            query.SortOrder ?? "",
            query.Statuses != null ? string.Join(",", query.Statuses) : "",
            query.RecruitmentFilter?.ToString() ?? "",
            query.ClosedReasonFilter?.ToString() ?? "",
            query.RequiredTags != null ? string.Join(",", query.RequiredTags) : "",
            query.OptionalTags != null ? string.Join(",", query.OptionalTags) : "",
            query.ExcludedTags != null ? string.Join(",", query.ExcludedTags) : "",
            query.OwnerUsernames != null ? string.Join(",", query.OwnerUsernames) : "",
            query.PlayerUsername ?? "",
            query.PlayerParticipation?.ToString() ?? "",
            query.Participating?.ToString() ?? "",
            query.CreatedFromUtc?.ToString("O") ?? "",
            query.CreatedToUtc?.ToString("O") ?? "",
            query.ActivatedFromUtc?.ToString("O") ?? "",
            query.ActivatedToUtc?.ToString("O") ?? "",
            query.ClosedFromUtc?.ToString("O") ?? "",
            query.ClosedToUtc?.ToString("O") ?? "",
            query.RecruitmentStartedFromUtc?.ToString("O") ?? "",
            query.RecruitmentStartedToUtc?.ToString("O") ?? "",
            query.PremoderationStatuses != null ? string.Join(",", query.PremoderationStatuses) : "",
            query.ExcludedMasterIds != null ? string.Join(",", query.ExcludedMasterIds.Order()) : ""
        };
        // The values themselves, length-prefixed: a hash or a plain join lets two queries share a page.
        return string.Join("|", parts.Select(p => $"{p.Length}:{p}"));
    }
}
