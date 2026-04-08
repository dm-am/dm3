namespace DM.Domain.Core.Identity;

/// <summary>
/// Service for encoding/decoding PublicId identifiers.
/// PublicId is a short, URL-friendly identifier generated from SerialNumber.
/// Format: 5 lowercase letters [a-z]{5} (excluding i/l/o for readability).
/// </summary>
public interface IPublicIdService
{
    /// <summary>
    /// Encodes a serial number into a 5-letter public ID.
    /// </summary>
    /// <param name="serialNumber">The serial number (must be positive)</param>
    /// <returns>A 5-letter lowercase string</returns>
    string Encode(int serialNumber);

    /// <summary>
    /// Decodes a public ID back to its serial number.
    /// </summary>
    /// <param name="publicId">The public ID to decode</param>
    /// <returns>The original serial number</returns>
    /// <exception cref="ArgumentException">If publicId is invalid</exception>
    int Decode(string publicId);

    /// <summary>
    /// Validates that a string is a valid public ID format.
    /// </summary>
    /// <param name="publicId">The string to validate</param>
    /// <returns>True if valid format, false otherwise</returns>
    bool IsValid(string publicId);
}
