namespace DM.Domain.Community.Features.Polls;

/// <summary>
/// How long the text fields of a poll may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class PollFieldLimits
{
    /// <summary>Longest poll description.</summary>
    public const int DetailsMaxLength = 1000;
}
