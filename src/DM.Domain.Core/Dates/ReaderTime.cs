using System;
using System.Globalization;

namespace DM.Domain.Core.Dates;

/// <summary>
/// A moment written the way the reader of a letter keeps time.
/// </summary>
/// <remarks>
/// On the site a date is formatted by the browser, which knows where its reader
/// is. A letter has no browser, so the account carries the zone instead, and
/// this is the one place that turns a moment into the line a letter prints.
///
/// The offset travels with the time and is not decoration. A zone can be stale
/// — the reader moved and has not said so yet — and then the time is wrong by
/// hours with nothing on the page admitting it. Named, it is a time the reader
/// can convert; unnamed, it is a time they have to trust.
/// </remarks>
public static class ReaderTime
{
    /// <summary>The spelling of a moment in every letter that prints one.</summary>
    private const string Pattern = "dd.MM.yyyy HH:mm";

    /// <summary>
    /// The moment in the reader's zone, with the zone named after it.
    /// </summary>
    /// <param name="moment">The moment, as the server recorded it.</param>
    /// <param name="timeZoneId">
    /// IANA name of the reader's zone ("Europe/Moscow"). Empty, unknown to this
    /// machine, or absent means UTC: a letter that guessed would be wrong in a
    /// way the reader cannot see.
    /// </param>
    public static string Format(DateTimeOffset moment, string? timeZoneId)
    {
        var zone = Resolve(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(moment, zone);
        return $"{local.ToString(Pattern, CultureInfo.InvariantCulture)} ({Name(local.Offset)})";
    }

    /// <summary>
    /// Whether the name is an IANA zone ("Europe/Moscow") this machine knows.
    /// </summary>
    /// <remarks>
    /// What an account may be given as its zone: the browser names its zone this
    /// way, and a letter can print a time in it. A Windows name of a zone is found
    /// on some machines and not on others, so it is no name to keep.
    /// </remarks>
    public static bool IsKnownZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) &&
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone) &&
        zone.HasIanaId;

    /// <summary>The zone by name, or UTC when this machine has no such zone.</summary>
    private static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// The offset as a reader writes it: "UTC", "UTC+3", "UTC-3:30".
    /// </summary>
    /// <remarks>
    /// Not the zone's own name. "Europe/Moscow" is a database key, and the
    /// abbreviations people use for zones collide across the world; an offset
    /// from UTC is the one form that means the same thing everywhere.
    /// </remarks>
    private static string Name(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "UTC";

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var size = offset.Duration();
        return size.Minutes == 0
            ? $"UTC{sign}{size.Hours}"
            : $"UTC{sign}{size.Hours}:{size.Minutes:00}";
    }
}
