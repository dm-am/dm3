using System.Collections.Generic;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Community.Features.Statistics;

/// <summary>
/// The periods the leaderboards are counted over: a calendar month, a calendar
/// year, or all time.
/// </summary>
public static class LeaderboardPeriod
{
    /// <summary>The year of the all-time period</summary>
    public const int AllTime = 0;

    // Wide sanity bounds, not the life of the site: only a year that would
    // overflow the date arithmetic of the period window is refused.
    public const int MinYear = 1900;
    public const int MaxYear = 2100;

    /// <summary>
    /// The fields of a period that cannot be counted, with what is wrong with
    /// each; empty for a period that can.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Refusals(int year, int? month)
    {
        var errors = new Dictionary<string, string>();

        if (year != AllTime && (year < MinYear || year > MaxYear))
            errors["year"] = RefusalMessage.PeriodYearOutOfRange(MinYear, MaxYear);

        if (month is < 1 or > 12)
            errors["month"] = RefusalMessage.PeriodMonthOutOfRange;

        if (month.HasValue && year == AllTime)
            errors["year"] = RefusalMessage.PeriodMonthNeedsYear;

        return errors;
    }
}
