namespace DM.Domain.Community.Features.Catalogs;

/// <summary>
/// The one scale of tiers, shared by the award and the achievement catalogs: a tier
/// names the metal of the tile, nothing else.
/// </summary>
/// <remarks>
/// The numbering is the awards' table of metals, where 1 to 3 are the places of a
/// contest. An achievement chain climbs bronze, silver, gold, platinum, so its tiers
/// run 3, 2, 1, 5 by threshold; the numeral on its tile is the position in the chain,
/// not the tier. Steel carries no place, no record uses it, and the site draws it
/// without a metal.
/// </remarks>
public static class TierScale
{
    public const int Gold = 1;
    public const int Silver = 2;
    public const int Bronze = 3;
    public const int Steel = 4;
    public const int Platinum = 5;

    /// <summary>Lowest tier a catalog record may carry.</summary>
    public const int MinTier = Gold;

    /// <summary>Highest tier a catalog record may carry.</summary>
    /// <remarks>
    /// The single source the validators read: the edit form resends the tier it
    /// was handed, so a bound behind the catalog would refuse every change of that record.
    /// </remarks>
    public const int MaxTier = Platinum;
}
