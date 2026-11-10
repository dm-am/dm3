using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DM.Domain.Core.Mail;

namespace DM.Infrastructure.Mail;

/// <summary>
/// A letter as the payload of its outbox row: the one text form the mail sender writes and the
/// mail worker reads.
/// </summary>
/// <remarks>
/// JSON in a text column, not a JSON column: JSON escapes a zero character in the text, while the
/// JSON type of PostgreSQL refuses one, and letters carry text typed by users.
/// </remarks>
public static class LetterPayload
{
    private static readonly JsonSerializerOptions Codec = new(JsonSerializerDefaults.Web);

    /// <summary>The letter as its row stores it.</summary>
    public static string Write(EmailLetter letter) => JsonSerializer.Serialize(letter, Codec);

    /// <summary>
    /// Reads the letter of a row, or answers that its text is not a letter.
    /// </summary>
    public static bool TryRead(string? payload, [NotNullWhen(true)] out EmailLetter? letter)
    {
        letter = null;
        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }

        try
        {
            letter = JsonSerializer.Deserialize<EmailLetter>(payload, Codec);
        }
        catch (JsonException)
        {
            // Not a letter: no retry turns this text into one.
            return false;
        }

        return letter is not null;
    }
}
