using System.Text;

namespace DM.Domain.Core.Identity;

/// <summary>
/// Implementation of PublicId encoding.
/// Uses a 23-character alphabet (a-z minus i/l/o for readability).
/// Minimum output length is 5 characters, padded with 'a' if needed.
/// </summary>
public class PublicIdService
{
    /// <summary>
    /// Alphabet for encoding (23 chars: a-z without i, l, o to avoid confusion with 1, I, 0, O)
    /// </summary>
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Minimum length of encoded public ID: the first 23^5 = 6 436 343 serials all take five
    /// letters, so an address keeps its width as the site grows, and a shorter string is no code.
    /// </summary>
    private const int MinLength = 5;

    private static readonly int Base = Alphabet.Length; // 23

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
