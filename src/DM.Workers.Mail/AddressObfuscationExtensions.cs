using System.Linq;

namespace DM.Workers.Mail;

/// <summary>
/// Extensions for address logging obfuscation
/// </summary>
public static class AddressObfuscationExtensions
{
    /// <summary>
    /// Obfuscate email address for logs
    /// </summary>
    /// <param name="emailAddress"></param>
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
        // A part this short is answered with itself, because the crop hides nothing
        // when it can be built and cannot be built when there is nothing to hide. It
        // reads the last character of the input, which the empty local part of an
        // address like "@host" does not have - and this runs on the way into the line
        // the worker logs about a letter, before anything has parsed the address it
        // was complaining about, so the throw came out of the logging rather than out
        // of the delivery. A single character it used to answer with "a..a": longer
        // than what it was given, and a repetition of the one character it hid.
        if (input.Length <= Kept)
        {
            return input;
        }

        return $"{input[..2]}..{input[^1]}";
    }
}
