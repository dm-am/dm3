using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DM.Domain.Account.Features.TwoFactor;

/// <inheritdoc />
/// <remarks>
/// Sixteen symbols out of an alphabet of thirty-two: eighty bits per code, and
/// the number is calculated rather than picked. The secret of the factor is shut
/// behind the application key, so a recovery code is the one thing a reader of a
/// database dump can attack without it, and the hash is a fast one. Forty bits,
/// ten codes to a person, is hours of one graphics card; eighty is not.
///
/// A slow hash was the other way to close it and was rejected: up to ten Argon2
/// runs per sign-in attempt is a denial-of-service lever sitting on the
/// authentication path. Entropy costs nothing at verification time.
///
/// No salt, for the reason the confirmation links carry none: the input is our
/// own randomness rather than a password a person chose, so there is nothing to
/// slow a guess of and no table to build.
/// </remarks>
internal class RecoveryCodeFactory : IRecoveryCodeFactory
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

    /// <inheritdoc />
    public IReadOnlyList<string> Create(int count)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            codes.Add(CreateOne());
        }

        return codes;
    }

    private static string CreateOne()
    {
        // Five bits per symbol, taken out of the buffer without any reduction
        // modulo the alphabet: the alphabet is exactly thirty-two symbols wide,
        // so every five-bit value names one of them and the distribution is flat
        // by construction.
        var bits = CodeLength * 5;
        var buffer = RandomNumberGenerator.GetBytes((bits + 7) / 8);
        var symbols = new char[CodeLength];

        for (var i = 0; i < CodeLength; i++)
        {
            var offset = i * 5;
            var index = 0;
            for (var bit = 0; bit < 5; bit++)
            {
                var position = offset + bit;
                var value = (buffer[position / 8] >> (7 - position % 8)) & 1;
                index = (index << 1) | value;
            }

            symbols[i] = Alphabet[index];
        }

        return new string(symbols);
    }

    /// <inheritdoc />
    public string Normalize(string code)
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

    /// <inheritdoc />
    public byte[] Hash(string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)));
}
