using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DM.Architecture.Tests;

/// <summary>
/// An object initializer read out of a source file: where it ends, what it holds
/// and the name every member of it is written under.
/// </summary>
/// <remarks>
/// Two rules read the same anonymous payloads — the one that holds every field a
/// notification shows its reader to a Russian name, and the one that holds a
/// notification the client turns into a link to the field that link is built
/// from. Both need the same three answers out of the text: which brace closes
/// the one that opened, which commas belong to the initializer rather than to a
/// call inside it, and what each member is called. A second copy of that parser
/// is a second place to correct when one of them is wrong, and until somebody
/// notices, the two disagree about the same payload while both stay green.
///
/// It reads the one shape a payload is written in: a member either carries a
/// name of its own or borrows the last segment of the expression it is assigned
/// from, which is what the compiler does with it. A payload assembled anywhere
/// but in an initializer is not read here at all, and the rules built on this
/// say so by failing rather than by passing over it.
/// </remarks>
internal static class ObjectInitializer
{
    /// <summary>"Name = expression": a member of the payload spelled out.</summary>
    private static readonly Regex NamedMember = new(
        @"^\s*([A-Za-z_]\w*)\s*=(?!=)", RegexOptions.Compiled);

    /// <summary>"source.Name": a member that borrows its name from the source.</summary>
    private static readonly Regex BorrowedMember = new(
        @"\.\s*([A-Za-z_]\w*)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Members of one object initializer, both spellings of a member.
    /// </summary>
    /// <param name="body">The text between the braces of the initializer.</param>
    internal static IEnumerable<string> Members(string body)
    {
        foreach (var member in TopLevelParts(body))
        {
            var named = NamedMember.Match(member);
            if (named.Success)
            {
                yield return named.Groups[1].Value;
                continue;
            }

            var borrowed = BorrowedMember.Match(member.Trim());
            if (borrowed.Success)
            {
                yield return borrowed.Groups[1].Value;
            }
        }
    }

    /// <summary>
    /// Splits an initializer at the commas that belong to it, leaving alone those
    /// inside a nested call, collection or literal.
    /// </summary>
    /// <param name="body">The text between the braces of the initializer.</param>
    internal static IEnumerable<string> TopLevelParts(string body)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var symbol = body[i];
            if (symbol is '"' or '\'')
            {
                i = EndOfLiteral(body, i);
            }
            else if (symbol is '{' or '(' or '[')
            {
                depth++;
            }
            else if (symbol is '}' or ')' or ']')
            {
                depth--;
            }
            else if (symbol == ',' && depth == 0)
            {
                yield return body.Substring(start, i - start);
                start = i + 1;
            }
        }

        yield return body.Substring(start);
    }

    /// <summary>Index of the brace that closes the one opened at the given position.</summary>
    /// <param name="source">The text the initializer is written in.</param>
    /// <param name="opening">Index of the brace that opens it.</param>
    internal static int ClosingBrace(string source, int opening)
    {
        var depth = 0;
        for (var i = opening; i < source.Length; i++)
        {
            var symbol = source[i];
            if (symbol is '"' or '\'')
            {
                i = EndOfLiteral(source, i);
            }
            else if (symbol == '{')
            {
                depth++;
            }
            else if (symbol == '}' && --depth == 0)
            {
                return i;
            }
        }

        throw new InvalidOperationException("An object initializer is left open.");
    }

    /// <summary>
    /// Index of the quote that closes the literal opened at the given position, so
    /// that a brace or a comma inside it is not read as punctuation.
    /// </summary>
    private static int EndOfLiteral(string source, int opening)
    {
        var quote = source[opening];
        for (var i = opening + 1; i < source.Length; i++)
        {
            if (source[i] == '\\')
            {
                i++;
            }
            else if (source[i] == quote)
            {
                return i;
            }
        }

        return source.Length - 1;
    }
}
