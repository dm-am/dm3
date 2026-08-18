using System.Text;

namespace DM.Domain.Core.Identity;

/// <summary>
/// Implementation of PublicId encoding/decoding.
/// Uses a 23-character alphabet (a-z minus i/l/o for readability).
/// Minimum output length is 5 characters, padded with 'a' if needed.
/// </summary>
public class PublicIdService : IPublicIdService
{
    /// <summary>
    /// Alphabet for encoding (23 chars: a-z without i, l, o to avoid confusion with 1, I, 0, O)
    /// </summary>
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Minimum length of encoded public ID
    /// </summary>
    private const int MinLength = 5;

    private static readonly int Base = Alphabet.Length; // 23

    /// <inheritdoc />
    public string Encode(int serialNumber)
    {
        if (serialNumber <= 0)
        {
            throw new ArgumentException("Serial number must be positive", nameof(serialNumber));
        }

        var result = new StringBuilder();
        var number = serialNumber - 1; // Convert to 0-based

        do
        {
            result.Insert(0, Alphabet[number % Base]);
            number /= Base;
        } while (number > 0);

        // Pad with 'a' (represents 0) to reach minimum length
        while (result.Length < MinLength)
        {
            result.Insert(0, 'a');
        }

        return result.ToString();
    }

    /// <summary>
    /// Decodes a public ID back to its serial number.
    /// </summary>
    /// <param name="publicId">The public ID to decode</param>
    /// <returns>The original serial number</returns>
    /// <exception cref="ArgumentException">If publicId is invalid</exception>
    public int Decode(string publicId)
    {
        if (!IsValid(publicId))
        {
            throw new ArgumentException($"Invalid public ID format: {publicId}", nameof(publicId));
        }

        // Remove leading 'a' padding
        var trimmed = publicId.TrimStart('a');

        // All 'a's means serial number 1
        if (string.IsNullOrEmpty(trimmed))
        {
            return 1;
        }

        var result = 0;
        foreach (var c in trimmed)
        {
            var index = Alphabet.IndexOf(c);
            result = result * Base + index;
        }

        return result + 1; // Convert back to 1-based
    }

    /// <inheritdoc />
    public bool IsValid(string publicId)
    {
        if (string.IsNullOrEmpty(publicId) || publicId.Length < MinLength)
        {
            return false;
        }

        foreach (var c in publicId)
        {
            if (!Alphabet.Contains(c))
            {
                return false;
            }
        }

        return true;
    }
}
