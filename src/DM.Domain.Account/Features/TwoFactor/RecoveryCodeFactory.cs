using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The values that let a person in when the device is gone.
/// </summary>
/// <remarks>
/// Eighty bits per code, because a reader of a database dump can attack the fast,
/// unsalted hash; a slow hash on the sign-in path would be a denial-of-service lever.
/// </remarks>
internal static class RecoveryCodeFactory
{
    /// <summary>
    /// Crockford's alphabet: no I, no L, no O, no U.
    /// </summary>
    /// <remarks>
    /// The first three are the characters a handwritten code loses to the digits
    /// beside them; the fourth is left out so that no set of codes accidentally
    /// spells something.
    /// </remarks>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Symbols one code is made of.</summary>
    internal const int CodeLength = 16;

    /// <summary>How many symbols are shown between separators.</summary>
    internal const int GroupSize = 4;

    /// <summary>
    /// A fresh set of codes, in the clear.
    /// </summary>
    /// <remarks>
    /// The only moment these exist as values: the caller shows them once and
    /// stores nothing but their hashes.
    /// </remarks>
    /// <param name="count">How many codes the set holds</param>
    public static IReadOnlyList<string> Create(int count)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            codes.Add(CreateOne());
        }

        return codes;
    }

    private static string CreateOne() => RandomNumberGenerator.GetString(Alphabet, CodeLength);

    /// <summary>
    /// The form a code is compared and stored in.
    /// </summary>
    /// <remarks>
    /// Case and separators are dropped, and the letters the alphabet
    /// leaves out are folded onto the digits they are mistaken for. A code
    /// copied out by hand onto paper and typed back with dashes and a lowercase
    /// l has to be the same code.
    /// </remarks>
    /// <param name="code">Value as the person typed it</param>
    public static string Normalize(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        var normalized = new StringBuilder(code.Length);
        foreach (var character in code)
        {
            var upper = char.ToUpperInvariant(character);
            var mapped = upper switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                _ => upper
            };

            if (Alphabet.IndexOf(mapped, StringComparison.Ordinal) >= 0)
            {
                normalized.Append(mapped);
            }
        }

        return normalized.ToString();
    }

    /// <summary>
    /// What the database keeps for a code.
    /// </summary>
    /// <param name="code">Value as the person typed it</param>
    public static byte[] Hash(string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)));
}
