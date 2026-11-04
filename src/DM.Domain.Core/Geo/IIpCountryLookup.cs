namespace DM.Domain.Core.Geo;

/// <summary>
/// The country an address belongs to, as far as a local table knows.
/// </summary>
/// <remarks>
/// The country and not the city: a city by address is wrong often enough to cost
/// a needless password reset.
/// </remarks>
public interface IIpCountryLookup
{
    /// <summary>
    /// The country of an address, or null when it is unknown, unparseable or
    /// there is no table to ask.
    /// </summary>
    /// <param name="ipAddress">Address as it was recorded, may be null.</param>
    string? Find(string? ipAddress);
}
