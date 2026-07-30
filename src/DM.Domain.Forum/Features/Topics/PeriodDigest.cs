namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// The closed statistics period an auto-generated "Итоги …" topic summarizes
/// </summary>
public class PeriodDigest
{
    /// <summary>
    /// Year of the summarized period
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Month of the summarized period, null for a yearly digest
    /// </summary>
    public int? Month { get; set; }
}
