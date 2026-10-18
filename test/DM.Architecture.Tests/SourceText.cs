using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace DM.Architecture.Tests;

/// <summary>
/// Source read as text, with the comments taken out.
/// </summary>
/// <remarks>
/// This tier asserts properties that belong to the code and would otherwise need
/// the whole stack up to observe — the order of the middleware, the absence of a
/// machine-clock read, the determinism of the seed. Reading the file is the only
/// way to get at them, and a file is not code: a paragraph explaining why a call
/// is absent contains the call, and two slashes in front of a line neither move
/// it nor remove the literal on it.
///
/// The order gate is where that stopped being a detail. It compares the
/// positions of three calls in Startup.cs, and a commented-out pipeline
/// satisfies every one of those comparisons — a green build over an application
/// whose per-account rate limits had silently become per-address ones, which is
/// exactly what its own remarks say nobody would notice otherwise.
///
/// One spelling, in one place, because five copies of it existed and the sixth
/// check copied none of them. It lives in this project rather than in DM.Testing
/// on purpose: a text helper has no reason to depend on NSubstitute and EF, and a
/// reference to that project would put NSubstitute in the output directory the ArchUnit
/// loader scans. It moves to a shared home when a consumer outside this tier
/// appears, rather than being copied to it.
/// </remarks>
internal static class SourceText
{
    private static readonly Regex Comments = new(
        @"/\*.*?\*/|//[^\n]*", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// The file's code, with block and line comments blanked out.
    /// </summary>
    /// <remarks>
    /// Replaced with nothing rather than with spaces: every consumer either
    /// searches for a substring or compares relative positions, and removal
    /// shifts all of those equally.
    /// </remarks>
    /// <param name="path">Absolute path of the source file.</param>
    internal static string ReadCode(string path) =>
        Comments.Replace(File.ReadAllText(path), string.Empty);

    /// <summary>
    /// The slice from one marker up to the next, the opening marker included.
    /// </summary>
    /// <remarks>
    /// Both markers are asserted rather than answered around, because the rules
    /// that ask for a slice are asking about what is inside it: a marker that has
    /// been renamed away leaves an empty slice, and an empty slice contains
    /// nothing to complain about.
    /// </remarks>
    /// <param name="source">Text to slice</param>
    /// <param name="start">Marker the slice opens at</param>
    /// <param name="end">Marker the slice stops before</param>
    internal static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"the source must still declare {start}");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        to.Should().BeGreaterThan(-1, $"the declaration of {start} must be terminated");
        return source[from..to];
    }

    /// <summary>
    /// Signature followed by a body or an expression body. The capture keeps the
    /// parameter list, and the position of the match is where the body starts.
    /// </summary>
    private static readonly Regex Signature = new(
        @"(?<sig>(?:public|private|protected|internal)[^;{}()]*?\s(?<name>\w+)\s*\((?<args>[^)]*)\))\s*(?<open>=>|\{)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>One method of a type: what it is called, what it takes, what it does.</summary>
    internal readonly record struct Member(string Name, string Parameters, string Body);

    /// <summary>
    /// The methods a file declares, each with its body.
    /// </summary>
    /// <remarks>
    /// One parser, because two of them existed and the second was written by
    /// copying the first. Brace counting rather than a syntax tree: the rules that
    /// read this ask where a call sits relative to another call, which survives
    /// being approximate, and a parser dependency in this tier would put a compiler
    /// in the directory the ArchUnit loader scans.
    /// </remarks>
    internal static IReadOnlyList<Member> Members(string code)
    {
        var members = new List<Member>();
        foreach (Match match in Signature.Matches(code))
        {
            members.Add(new Member(
                match.Groups["name"].Value,
                match.Groups["args"].Value,
                BodyAfter(code, match)));
        }

        return members;
    }

    /// <summary>One method of a type and the lines it spans, signature to end.</summary>
    internal readonly record struct Span(string Name, int Line, int Lines);

    /// <summary>A header that opens a body holding declarations rather than statements.</summary>
    private static readonly Regex TypeHeader = new(
        @"\b(?:class|struct|interface|record|enum|namespace|extension)\b", RegexOptions.Compiled);

    /// <summary>What may follow a parameter list before a method's body.</summary>
    private static readonly Regex AfterParameters = new(
        @"^\s*(?:$|where\b|:\s*(?:base|this)\b)", RegexOptions.Compiled);

    /// <summary>
    /// Words that stand in front of a parenthesis without naming a method: the
    /// modifiers before a tuple return type, and the keywords that take one.
    /// </summary>
    private static readonly HashSet<string> NotNames = new(StringComparer.Ordinal)
    {
        "abstract", "async", "base", "catch", "checked", "default", "extension", "extern", "fixed",
        "for", "foreach", "if", "internal", "lock", "nameof", "new", "override", "partial",
        "private", "protected", "public", "readonly", "ref", "required", "return", "sealed",
        "sizeof", "static", "switch", "this", "typeof", "unchecked", "unsafe", "using", "virtual",
        "volatile", "when", "while",
    };

    /// <summary>
    /// The methods a file declares, each with the line its signature starts on and
    /// the number of lines from there to the end of its body.
    /// </summary>
    /// <remarks>
    /// A reader of its own rather than the <see cref="Signature"/> of
    /// <see cref="Members"/>. That one answers where a call sits and can afford to
    /// miss a declaration; a length limit that misses a declaration is no limit for
    /// it, and the shapes a pattern misses are common: a generic method, a tuple
    /// return type, a parenthesis inside the parameters, a constructor calling
    /// base, a method with no access modifier.
    ///
    /// So the file is read the way a type's body is laid out: a run of
    /// declarations, each ending at a semicolon, at a body, or at an expression
    /// body's semicolon. A body is a type's when its header names a type, and a
    /// method's when the header ends in a parameter list; anything else (accessors,
    /// an initializer) is stepped over. A local function or a lambda is part of
    /// the method around it, as its reader meets it.
    ///
    /// The file is read with its comments and literals blanked rather than removed:
    /// a comment inside a method is a line the reader of the method reads, and
    /// taking it out would move every line after it. Literals are blanked because
    /// one "{" in a string would carry a body on to the end of the file.
    /// </remarks>
    /// <param name="text">The file as written, comments included.</param>
    internal static IReadOnlyList<Span> MemberSpans(string text)
    {
        var code = Blank(text);
        var spans = new List<Span>();
        ReadDeclarations(code, 0, code.Length, spans);
        return spans;
    }

    /// <summary>The declarations between two positions of a body that holds declarations.</summary>
    private static void ReadDeclarations(string code, int from, int to, List<Span> spans)
    {
        var start = from;
        while (start < to)
        {
            var stop = DeclarationStop(code, start, to);
            if (stop >= to)
            {
                return;
            }

            var header = code[start..stop];
            var name = MethodName(header, out var lead);
            int end;
            if (code[stop] == '{')
            {
                end = Closing(code, stop);

                // A body that is neither a method's nor a type's, a property's
                // accessors or an object initializer, is stepped over: what follows
                // it up to its semicolon reads as a declaration without a body.
                if (name is null && TypeHeader.IsMatch(header))
                {
                    ReadDeclarations(code, stop + 1, end, spans);
                }
            }
            else
            {
                end = code[stop] == '=' ? StatementEnd(code, stop + 2, to) : stop;
            }

            if (name is not null && code[stop] is '{' or '=')
            {
                var line = LineAt(code, start + lead);
                spans.Add(new Span(name, line, LineAt(code, end) - line + 1));
            }

            start = end + 1;
        }
    }

    /// <summary>
    /// The first semicolon, brace or arrow outside any bracket, which ends the
    /// header of the declaration that starts at a position.
    /// </summary>
    private static int DeclarationStop(string code, int from, int to)
    {
        var depth = 0;
        for (var i = from; i < to; i++)
        {
            var c = code[i];
            if (depth == 0 && (c is ';' or '{' or '}' || IsArrow(code, i)))
            {
                return i;
            }

            depth += Nesting(c);
        }

        return to;
    }

    /// <summary>The first semicolon outside any bracket from a position, or the end.</summary>
    private static int StatementEnd(string code, int from, int to)
    {
        var depth = 0;
        for (var i = from; i < to; i++)
        {
            if (depth == 0 && code[i] == ';')
            {
                return i;
            }

            depth += Nesting(code[i]);
        }

        return to;
    }

    /// <summary>The bracket closing the one that opens at a position.</summary>
    private static int Closing(string code, int open)
    {
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            depth += Nesting(code[i]);
            if (depth == 0)
            {
                return i;
            }
        }

        return code.Length;
    }

    /// <summary>One bracket count for all three kinds: in code that compiles they nest.</summary>
    private static int Nesting(char c) => c switch
    {
        '(' or '[' or '{' => 1,
        ')' or ']' or '}' => -1,
        _ => 0,
    };

    private static bool IsArrow(string code, int i) =>
        code[i] == '=' && i + 1 < code.Length && code[i + 1] == '>';

    /// <summary>
    /// Whether a header assigns outside its brackets: a field or property with an
    /// initializer, as against the comparison and arrow operators.
    /// </summary>
    private static bool Assigns(string header)
    {
        var depth = 0;
        for (var i = 0; i < header.Length; i++)
        {
            var c = header[i];
            depth += Nesting(c);
            if (depth == 0 && c == '=' && !IsArrow(header, i)
                && (i == 0 || header[i - 1] is not ('=' or '!' or '<' or '>'))
                && (i + 1 == header.Length || header[i + 1] != '='))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The name of the method a header declares, or null when it declares
    /// something else; with it, where the declaration starts past its attributes.
    /// </summary>
    private static string? MethodName(string header, out int lead)
    {
        lead = 0;
        while (lead < header.Length && (char.IsWhiteSpace(header[lead]) || header[lead] == '['))
        {
            lead = header[lead] == '[' ? Closing(header, lead) + 1 : lead + 1;
        }

        for (var open = lead; open < header.Length; open++)
        {
            if (header[open] is '[')
            {
                open = Closing(header, open);
            }
            else if (header[open] is '(')
            {
                var name = NameBefore(header, open, out var nameStart);
                var close = Closing(header, open);
                if (name is not null && !TypeHeader.IsMatch(header[lead..nameStart])
                    && !Assigns(header[lead..nameStart]))
                {
                    return close < header.Length && AfterParameters.IsMatch(header[(close + 1)..]) ? name : null;
                }

                open = close;
            }
        }

        return null;
    }

    /// <summary>
    /// The identifier just in front of a parenthesis, past a list of generic
    /// parameters, or null when what stands there names no method.
    /// </summary>
    private static string? NameBefore(string header, int open, out int start)
    {
        var i = open - 1;
        while (i >= 0 && char.IsWhiteSpace(header[i]))
        {
            i--;
        }

        if (i >= 0 && header[i] == '>')
        {
            for (var depth = 0; i >= 0; i--)
            {
                depth += header[i] switch { '>' => 1, '<' => -1, _ => 0 };
                if (depth == 0)
                {
                    break;
                }
            }

            i--;
            while (i >= 0 && char.IsWhiteSpace(header[i]))
            {
                i--;
            }
        }

        // An operator such as >> has no list of generic parameters to step past.
        if (i < -1)
        {
            start = 0;
            return null;
        }

        var end = i + 1;
        while (i >= 0 && (char.IsLetterOrDigit(header[i]) || header[i] == '_'))
        {
            i--;
        }

        start = i + 1;
        var name = header[start..end];
        return name.Length > 0 && !char.IsDigit(name[0]) && !NotNames.Contains(name) ? name : null;
    }

    /// <summary>
    /// The text with comments, preprocessor directives and string and character
    /// literals turned into spaces, newlines kept, so that what is left is code at
    /// the place the file has it.
    /// </summary>
    private static string Blank(string text)
    {
        var code = text.Replace("\r\n", "\n", StringComparison.Ordinal).ToCharArray();
        var i = 0;
        while (i < code.Length)
        {
            var end = TokenEnd(code, i);
            if (end < 0)
            {
                i++;
                continue;
            }

            for (var k = i; k < end; k++)
            {
                if (code[k] != '\n')
                {
                    code[k] = ' ';
                }
            }

            i = end;
        }

        return new string(code);
    }

    /// <summary>
    /// Where the comment, directive or literal opening at a position ends,
    /// exclusive, or -1 when none opens there.
    /// </summary>
    private static int TokenEnd(char[] code, int i)
    {
        var next = i + 1 < code.Length ? code[i + 1] : '\0';
        switch (code[i])
        {
            case '/' when next == '/':
                return Find(code, i, "\n", include: false);
            case '/' when next == '*':
                return Find(code, i + 2, "*/", include: true);
            // A #region line is not part of the declaration below it.
            case '#' when StartsLine(code, i):
                return Find(code, i, "\n", include: false);
            case '\'':
                return Escaped(code, i, '\'');
            // Verbatim first: in @""" the second and third quotes are an escaped
            // quote, not the opening of a raw literal.
            case '"' when IsVerbatim(code, i):
                return Verbatim(code, i);
            case '"':
                var quotes = 0;
                while (i + quotes < code.Length && code[i + quotes] == '"')
                {
                    quotes++;
                }

                return quotes >= 3
                    ? Find(code, i + quotes, new string('"', quotes), include: true)
                    : Escaped(code, i, '"');
            default:
                return -1;
        }
    }

    private static bool StartsLine(char[] code, int i)
    {
        var k = i - 1;
        while (k >= 0 && code[k] is ' ' or '\t')
        {
            k--;
        }

        return k < 0 || code[k] == '\n';
    }

    private static int Find(char[] code, int from, string marker, bool include)
    {
        var at = code.AsSpan(from).IndexOf(marker.AsSpan());
        return at < 0 ? code.Length : from + at + (include ? marker.Length : 0);
    }

    /// <summary>A quoted literal in which a backslash escapes the next character.</summary>
    private static int Escaped(char[] code, int open, char quote)
    {
        for (var k = open + 1; k < code.Length; k++)
        {
            if (code[k] == '\\')
            {
                k++;
            }
            else if (code[k] == quote || code[k] == '\n')
            {
                return k + 1;
            }
        }

        return code.Length;
    }

    private static bool IsVerbatim(char[] code, int quote)
    {
        for (var k = quote - 1; k >= 0 && code[k] is '@' or '$'; k--)
        {
            if (code[k] == '@')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A verbatim literal, in which a doubled quote is a quote.</summary>
    private static int Verbatim(char[] code, int open)
    {
        for (var k = open + 1; k < code.Length; k++)
        {
            if (code[k] != '"')
            {
                continue;
            }

            if (k + 1 < code.Length && code[k + 1] == '"')
            {
                k++;
                continue;
            }

            return k + 1;
        }

        return code.Length;
    }

    private static int LineAt(string code, int index) =>
        code.AsSpan(0, Math.Min(index, code.Length)).Count('\n') + 1;

    /// <summary>
    /// Everything after a signature: up to the matching closing brace, or, for an
    /// expression body, up to the semicolon that ends the statement.
    /// </summary>
    private static string BodyAfter(string code, Match signature) =>
        code[(signature.Index + signature.Length)..BodyEnd(code, signature)];

    /// <summary>Where the body after a signature ends, exclusive.</summary>
    private static int BodyEnd(string code, Match signature)
    {
        var start = signature.Index + signature.Length;
        if (signature.Groups["open"].Value == "=>")
        {
            var end = code.IndexOf(';', start);
            return end < 0 ? code.Length : end;
        }

        var depth = 1;
        for (var i = start; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return i;
            }
        }

        return code.Length;
    }
}
