using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DM.Architecture.Tests;

/// <remarks>
/// Shared by the two rules that read notification payloads. A member carries a name of its own or
/// borrows the last segment of its expression; a payload built outside an initializer fails them.
/// </remarks>
internal static class ObjectInitializer
{
    private static readonly Regex NamedMember = new(
        @"^\s*([A-Za-z_]\w*)\s*=(?!=)", RegexOptions.Compiled);

    private static readonly Regex BorrowedMember = new(
        @"\.\s*([A-Za-z_]\w*)\s*$", RegexOptions.Compiled);

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
