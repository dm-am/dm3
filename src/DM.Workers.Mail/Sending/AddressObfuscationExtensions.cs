using System.Linq;

namespace DM.Workers.Mail.Sending;

/// <summary>
/// Extensions for address logging obfuscation
/// </summary>
public static class AddressObfuscationExtensions
{
    /// <summary>
    /// Obfuscate email address for logs
    /// </summary>
    /// <returns>Obfuscated email address</returns>
    public static string Obfuscate(this string emailAddress) =>
        string.Join('@',
            emailAddress.Split("@")
                .Select(CropMiddle));

    /// <summary>
    /// Characters the crop keeps: two at the front and one at the back. A part no
    /// longer than that has no middle left to hide.
    /// </summary>
    private const int Kept = 3;

    private static string CropMiddle(string input)
    {
        // Nothing to hide, and an empty part such as in "@host" has no last character.
        if (input.Length <= Kept)
        {
            return input;
        }

        return $"{input[..2]}..{input[^1]}";
    }
}
