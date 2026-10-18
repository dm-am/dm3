namespace DM.Domain.Core.Geo;

/// <summary>
/// The country an address belongs to, as far as a local table knows.
/// </summary>
/// <remarks>
/// One reader asks this and one letter prints it: the warning about a login
/// from a device the account has not seen. The address itself answers nothing
/// for the person reading — few know their own, and on a phone it changes with
/// the connection — while the country answers the only question the letter
/// asks, whether this was them.
///
/// The country and not the city. A city is wrong often enough to matter: a
/// mobile carrier hands out addresses from a regional hub, a corporate proxy
/// from another region entirely, and a wrong city in a letter about security
/// costs a password reset and a ticket. A country is wrong rarely, and the
/// difference the reader is looking for is a country-sized one.
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
