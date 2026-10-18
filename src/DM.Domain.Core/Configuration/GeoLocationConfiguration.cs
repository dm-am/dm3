namespace DM.Domain.Core.Configuration;

/// <summary>
/// Where the table of address ranges lives and where a fresh copy comes from.
/// </summary>
/// <remarks>
/// A path and not a bundled file: the table goes stale, its publisher refreshes
/// it daily, and a copy committed here would be out of date on the day after it
/// was committed. Left empty, the lookup answers nothing and the letter prints
/// no country, which is what every host that never sends that letter should do.
/// </remarks>
public class GeoLocationConfiguration
{
    /// <summary>
    /// Path to the CSV of address ranges: first address, last address, two-letter
    /// country code, one range per line. The host that refreshes the table writes
    /// each new copy here, so the directory has to be writable and survive a
    /// restart.
    /// </summary>
    public string CountryDatabasePath { get; set; } = string.Empty;

    /// <summary>
    /// Where the refresh downloads the table from: user-country of
    /// sapics/ip-location-db, the latest release. Empty turns the download off,
    /// and the file at <see cref="CountryDatabasePath"/> is then read as it is.
    /// </summary>
    /// <remarks>
    /// The only request that leaves the site for this feature. The address of a
    /// reader is looked up in the local copy and is never sent anywhere.
    /// </remarks>
    public string CountryDatabaseUrl { get; set; } =
        "https://github.com/sapics/ip-location-db/releases/download/latest/user-country.csv";
}
