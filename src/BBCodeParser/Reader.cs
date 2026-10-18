// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;
using System.Collections.Generic;
using BBCodeParser.Tags;

namespace BBCodeParser
{
    // DM3 change against the original: the reader is a scanner written by hand,
    // and it is the grammar of the markup. The original read tags with one
    // regular expression,
    //
    //     \[(?<closing>\/)?(?<tag>\w+)(\=\"(?<value>.*?)\")?\]
    //
    // which knew one attribute per tag, compared names in whatever casing the
    // tag set happened to use, and read a spelling it did not know as text -
    // including the spelling of a block that hides its content, which then
    // reached every reader. The rules below are the ones written down in
    // docs/architecture/BBCODE_RENDERING.md, section "Грамматика разметки", and
    // held to the rows of bbcodeGrammar.cases.ts by BbGrammarShould.
    //
    // A tag token is an opening bracket, an optional slash and a name made of
    // ASCII letters, followed by "]", "=", a whitespace character or the end of
    // the text. Anything else is a word in prose.
    //
    // Names of tags and of parameters are compared without regard to case, and
    // only A-Z folds onto a-z (see AsciiLower). [B] is [b], [/PRIVATE] closes
    // [private], WIDTH= is width=. The comparisons a language offers "ignoring
    // case" can fold more than that, and each side folds differently: in .NET
    // ToLowerInvariant and a regular expression ignoring case take the Kelvin
    // sign U+212A for k, a browser regular expression without the u flag does
    // not, and the two halves of this grammar - the server and the editor -
    // would then read one text two ways. A name is read from ASCII letters only,
    // so a letter of another alphabet makes the word prose on both sides.
    //
    // An opening tag is one of:
    //   [name]                       every tag;
    //   [name="value"]               a tag declaring the DM2 attribute; the value
    //                                runs to the first quote followed by "]";
    //   [name a="1" b="2"]           a tag declaring named parameters; names are
    //                                ASCII letters in any case, values are always
    //                                quoted, pairs are separated by one space or
    //                                more, each name at most once whatever its
    //                                case, in any order, and a value ends at a
    //                                quote followed by "]" or by spaces and the
    //                                next name=.
    // No value carries an opening bracket or a line break: a bracket starts the
    // next tag, and a value that reached one was never closed. There is no
    // escaping, and no other whitespace belongs in a tag.
    //
    // A token that names a tag and does not match its grammar is a broken tag.
    // It still becomes a node - the node renders as the text that was typed, and
    // its content renders normally (see TagNode) - and the text of its opening
    // runs to the first "]" when that comes before the next "[" (and, for a tag
    // that does not hide, before a line break); otherwise it is the name alone
    // and reading resumes right after it. A closing tag is exactly [/name] with
    // nothing else in it, and anything else that starts with "[/" is text.
    //
    // Linear time. Every scan below stops at the next opening bracket at the
    // latest, and the text after a token is read starting where the token ends,
    // so no character is looked at more than a small constant number of times.
    // The one scan that runs further, the search for the closing tag of a block
    // shown as written, remembers its answer per name (FindClosing). The shapes
    // that used to cost the square of the input are held by BbParserCostShould: a
    // name followed by thousands of spaces, many pairs, many quotes.
    public class Reader
    {
        private readonly string input;
        private readonly Dictionary<string, Tag> tagsByName;

        // Where the closing tag of a block shown as written was last looked for,
        // per tag name, and where it was found (-1: nowhere after that point).
        // See FindClosing.
        private readonly Dictionary<string, (int From, int At)> closings;

        private int position;
        private TagResult pending;

        public Reader(string input, Tag[] tags)
        {
            this.input = input;
            tagsByName = new Dictionary<string, Tag>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                tagsByName.TryAdd(AsciiLower(tag.Name), tag);
            }

            closings = new Dictionary<string, (int From, int At)>(StringComparer.Ordinal);
            position = 0;
        }

        /// <summary>
        /// The next token: a run of text (TagType.NoResult), an opening tag or a
        /// closing tag. False when the text is exhausted.
        /// </summary>
        public bool TryRead(out TagResult result)
        {
            if (pending != null)
            {
                result = pending;
                pending = null;
                return true;
            }

            if (position >= input.Length)
            {
                result = null;
                return false;
            }

            var scan = position;
            while (true)
            {
                var bracket = input.IndexOf('[', scan);
                if (bracket < 0)
                {
                    result = TextToken(position, input.Length);
                    position = input.Length;
                    return true;
                }

                var tag = ReadTagAt(bracket, out var end);
                if (tag == null)
                {
                    scan = bracket + 1;
                    continue;
                }

                if (bracket > position)
                {
                    result = TextToken(position, bracket);
                    pending = tag;
                }
                else
                {
                    result = tag;
                }

                position = end;
                return true;
            }
        }

        /// <summary>
        /// Read the content of a block shown as written, which the caller has
        /// just opened: everything up to the block's own closing tag, none of it
        /// markup. False, with nothing read, when no such closing tag follows.
        /// </summary>
        /// <remarks>
        /// Only the closing tag makes the block an example. A block the author
        /// never closed states nothing, and the caller reads what follows its
        /// opening as it reads any other text - hiding markup included, which
        /// then hides.
        /// </remarks>
        internal bool TryReadVerbatim(Tag tag, out TagResult content, out string closer)
        {
            var name = AsciiLower(tag.Name);
            var closingAt = FindClosing(name, position);
            if (closingAt < 0)
            {
                content = null;
                closer = null;
                return false;
            }

            var end = closingAt + name.Length + 3;
            content = TextToken(position, closingAt);
            closer = input.Substring(closingAt, end - closingAt);
            position = end;
            return true;
        }

        /// <summary>
        /// The first closing tag of the given name at or after the index, in any
        /// case, or -1.
        /// </summary>
        /// <remarks>
        /// Remembered per name, because every opening of a block nobody closed
        /// asks again, from further on in the same text, and a text of such
        /// openings would otherwise be scanned to its end once per opening. An
        /// answer of "nowhere" stays true for every later start, and one that
        /// found the tag stays true for every start up to it, so the text is
        /// scanned about once per name.
        /// </remarks>
        private int FindClosing(string name, int from)
        {
            if (closings.TryGetValue(name, out var known) && known.From <= from &&
                (known.At < 0 || known.At >= from))
            {
                return known.At;
            }

            var at = -1;
            for (var scan = from; ; scan++)
            {
                scan = input.IndexOf('[', scan);
                if (scan < 0)
                {
                    break;
                }

                if (IsClosingOf(name, scan))
                {
                    at = scan;
                    break;
                }
            }

            closings[name] = (from, at);
            return at;
        }

        /// <summary>Whether [/name] stands at the bracket, the name in any case.</summary>
        private bool IsClosingOf(string name, int bracket)
        {
            var end = bracket + name.Length + 3;
            if (end > input.Length || input[bracket + 1] != '/' || input[end - 1] != ']')
            {
                return false;
            }

            for (var index = 0; index < name.Length; index++)
            {
                if (AsciiLower(input[bracket + 2 + index]) != name[index])
                {
                    return false;
                }
            }

            return true;
        }

        private TagResult ReadTagAt(int bracket, out int end)
        {
            end = bracket;
            if (bracket + 1 >= input.Length)
            {
                return null;
            }

            return input[bracket + 1] == '/'
                ? ReadClosing(bracket, out end)
                : ReadOpening(bracket, out end);
        }

        private TagResult ReadClosing(int bracket, out int end)
        {
            end = bracket;
            var nameStart = bracket + 2;
            var nameEnd = LettersEnd(nameStart);
            if (nameEnd == nameStart || nameEnd >= input.Length || input[nameEnd] != ']')
            {
                return null;
            }

            if (!tagsByName.TryGetValue(AsciiLower(input.Substring(nameStart, nameEnd - nameStart)), out var tag))
            {
                return null;
            }

            end = nameEnd + 1;
            return new TagResult
            {
                TagType = TagType.Close,
                Tag = tag,
                Text = input.Substring(bracket, end - bracket)
            };
        }

        private TagResult ReadOpening(int bracket, out int end)
        {
            end = bracket;
            var nameStart = bracket + 1;
            var nameEnd = LettersEnd(nameStart);
            if (nameEnd == nameStart || !IsTokenEnd(nameEnd))
            {
                return null;
            }

            if (!tagsByName.TryGetValue(AsciiLower(input.Substring(nameStart, nameEnd - nameStart)), out var tag))
            {
                return null;
            }

            var broken = !TryReadRest(tag, nameEnd, out var attribute, out var parameters, out end);
            if (broken)
            {
                attribute = string.Empty;
                parameters = null;
                end = BrokenEnd(nameEnd, tag.Hides);
            }

            return new TagResult
            {
                TagType = TagType.Open,
                Tag = tag,
                Text = input.Substring(bracket, end - bracket),
                AttributeValue = attribute,
                Parameters = parameters,
                Broken = broken
            };
        }

        /// <summary>
        /// Read what follows the name of an opening tag, to its closing bracket.
        /// False when it does not match the grammar of the tag.
        /// </summary>
        private bool TryReadRest(Tag tag, int nameEnd, out string attribute,
            out IReadOnlyDictionary<string, string> parameters, out int end)
        {
            attribute = string.Empty;
            parameters = null;
            end = nameEnd;
            if (nameEnd >= input.Length)
            {
                return false;
            }

            switch (input[nameEnd])
            {
                case ']':
                    end = nameEnd + 1;
                    return true;
                case '=':
                    return tag.WithAttribute && TryReadAttribute(nameEnd + 1, out attribute, out end);
                case ' ':
                    return tag.Parameters.Count > 0 && TryReadParameters(tag, nameEnd, out parameters, out end);
                default:
                    return false;
            }
        }

        /// <summary>
        /// The DM2 attribute: a quote, then everything up to the first quote that
        /// the closing bracket follows.
        /// </summary>
        private bool TryReadAttribute(int start, out string value, out int end)
        {
            value = string.Empty;
            end = start;
            if (start >= input.Length || input[start] != '"')
            {
                return false;
            }

            for (var index = start + 1; index < input.Length; index++)
            {
                var character = input[index];
                if (character == '"' && index + 1 < input.Length && input[index + 1] == ']')
                {
                    value = input.Substring(start + 1, index - start - 1);
                    end = index + 2;
                    return true;
                }

                if (IsValueStop(character))
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Named parameters, starting at the space after the tag name.</summary>
        private bool TryReadParameters(Tag tag, int start, out IReadOnlyDictionary<string, string> read,
            out int end)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            read = null;
            end = start;
            var index = start;

            while (true)
            {
                if (index >= input.Length || input[index] != ' ')
                {
                    return false;
                }

                while (index < input.Length && input[index] == ' ')
                {
                    index++;
                }

                var parameterStart = index;
                while (index < input.Length && IsParameterLetter(input[index]))
                {
                    index++;
                }

                if (index == parameterStart || index >= input.Length || input[index] != '=')
                {
                    return false;
                }

                var parameterName = AsciiLower(input.Substring(parameterStart, index - parameterStart));
                index++;
                if (index >= input.Length || input[index] != '"')
                {
                    return false;
                }

                index++;
                var valueStart = index;
                var closesTag = false;
                while (true)
                {
                    if (index >= input.Length || IsValueStop(input[index]))
                    {
                        return false;
                    }

                    if (input[index] == '"')
                    {
                        if (index + 1 < input.Length && input[index + 1] == ']')
                        {
                            closesTag = true;
                            break;
                        }

                        if (StartsNextParameter(index + 1))
                        {
                            break;
                        }
                    }

                    index++;
                }

                if (!Accept(tag, parameters, parameterName, input.Substring(valueStart, index - valueStart)))
                {
                    return false;
                }

                if (closesTag)
                {
                    read = parameters;
                    end = index + 2;
                    return true;
                }

                index++;
            }
        }

        /// <summary>Whether spaces and the next name= stand at the index.</summary>
        private bool StartsNextParameter(int index)
        {
            if (index >= input.Length || input[index] != ' ')
            {
                return false;
            }

            while (index < input.Length && input[index] == ' ')
            {
                index++;
            }

            var nameStart = index;
            while (index < input.Length && IsParameterLetter(input[index]))
            {
                index++;
            }

            return index > nameStart && index < input.Length && input[index] == '=';
        }

        private static bool Accept(Tag tag, Dictionary<string, string> parameters, string name, string value)
        {
            if (!tag.Parameters.TryGetValue(name, out var kind) || parameters.ContainsKey(name))
            {
                return false;
            }

            if (kind == ParameterKind.Size && !IsSize(value))
            {
                return false;
            }

            parameters[name] = value;
            return true;
        }

        /// <summary>1 to 9999, digits only, no leading zero.</summary>
        private static bool IsSize(string value)
        {
            if (value.Length == 0 || value.Length > 4 || value[0] == '0')
            {
                return false;
            }

            foreach (var character in value)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Where the text of a broken opening tag ends: at the first "]" that
        /// comes before the next "[" (and before a line break, for a tag that
        /// does not hide), otherwise right after the name.
        /// </summary>
        private int BrokenEnd(int nameEnd, bool hides)
        {
            for (var index = nameEnd; index < input.Length; index++)
            {
                var character = input[index];
                if (character == ']')
                {
                    return index + 1;
                }

                if (character == '[' || (!hides && (character == '\n' || character == '\r')))
                {
                    return nameEnd;
                }
            }

            return nameEnd;
        }

        /// <summary>The name with A-Z folded onto a-z and every other character left as it is.</summary>
        private static string AsciiLower(string name)
        {
            foreach (var character in name)
            {
                if (character >= 'A' && character <= 'Z')
                {
                    return string.Create(name.Length, name, static (span, source) =>
                    {
                        for (var index = 0; index < source.Length; index++)
                        {
                            span[index] = AsciiLower(source[index]);
                        }
                    });
                }
            }

            return name;
        }

        private static char AsciiLower(char character) =>
            character >= 'A' && character <= 'Z' ? (char)(character + ('a' - 'A')) : character;

        private int LettersEnd(int index)
        {
            while (index < input.Length && IsNameLetter(input[index]))
            {
                index++;
            }

            return index;
        }

        private bool IsTokenEnd(int index) =>
            index >= input.Length || input[index] == ']' || input[index] == '=' || char.IsWhiteSpace(input[index]);

        private TagResult TextToken(int start, int end) => new TagResult
        {
            TagType = TagType.NoResult,
            Text = input.Substring(start, end - start)
        };

        private static bool IsNameLetter(char character) =>
            (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z');

        private static bool IsParameterLetter(char character) => IsNameLetter(character);

        private static bool IsValueStop(char character) =>
            character == '[' || character == '\n' || character == '\r';
    }
}
