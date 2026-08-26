// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System.Linq;
using System.Text.RegularExpressions;
using BBCodeParser.Tags;

namespace BBCodeParser
{
    public class Reader
    {
        private readonly string input;
        private readonly Tag[] tags;
        private int position;
        private Match match;

        // DM3 change against the original. The pattern was
        //
        //     \[(?<closing>\/)?(?<tag>\w+)(\=\"(?<value>.*?)\")?\]
        //
        // and three separate defects came out of those ten characters in the
        // middle.
        //
        // 1. Cost. The lazy .*? had no terminator inside it, so on an input
        //    where =" opens and never closes - [quote=" repeated - every one of
        //    those positions scanned the whole remaining text before failing.
        //    Sixty-four kilobytes of it cost twenty-nine seconds of CPU, on a
        //    render path that is not cached and that every reader of the page
        //    pays. The value classes below all stop at [, which is the
        //    character that starts the next tag and therefore cannot be part of
        //    an attribute value that is still open; the scan is bounded by the
        //    distance to it and the whole pass is linear in the input.
        //
        // 2. Line breaks. The dot does not match \n without Singleline, so
        //    [private="Gen\ndalf"] stopped being a tag at all. The save-time
        //    validator accepted such a post and the renderer emitted the
        //    private text as ordinary text to everyone in the room. The quoted
        //    value class below is negated, so a line break inside quotes is
        //    just another character and the tag stays a tag. (An unquoted value
        //    still stops at a line break: there the closing bracket is the only
        //    terminator, and letting one run over lines would swallow whole
        //    paragraphs into an attribute.)
        //
        // 3. Unquoted values. Only ="value" was recognised, so [quote=Vasya]
        //    was not a tag - and worse, its [/quote] was still eaten, so the
        //    author saw the text and the reader saw the markup. Everything the
        //    editor writes in that shape had to be cut out of the text by
        //    regular expressions before the parser ever saw it. The bare
        //    alternative below reads a value up to the closing bracket.
        //
        // The atomic groups are not decoration: once a value class has run to
        // the character that stops it, no shorter prefix of it can make the
        // rest of the pattern match, so backtracking into it is always wasted
        // work - and on adversarial input it is quadratic wasted work.
        //
        // Kept from the original on purpose: a value may contain a double quote
        // as long as it is not the one before the closing bracket, so
        // [quote="a"b"] still carries a"b.
        private static readonly Regex BbPattern = new Regex(
            @"\[(?<closing>/)?(?>(?<tag>\w+))" +
            @"(?:=(?:""(?>(?<quoted>(?:[^""\[]|""(?!\]))*))""|(?>(?<bare>[^\[\]""\r\n]*))))?\]",
            RegexOptions.Compiled);

        public Reader(string input, Tag[] tags)
        {
            this.input = input;
            this.tags = tags;
            position = 0;
            match = BbPattern.Match(this.input);
        }

        public bool TryRead(out TagResult result)
        {
            if (position == input.Length)
            {
                result = null;
                return false;
            }

            result = new TagResult();
            if (match.Success)
            {
                var tagName = match.Groups["tag"].Value;
                var matchingTag = tags.FirstOrDefault(t => t.Name == tagName);
                var bare = match.Groups["bare"];

                // DM3 change against the original: the unquoted attribute form
                // is honoured only by a tag that declares one. Every spelling
                // the original recognised is still recognised exactly as it
                // was - including the quoted value on a tag with no attribute,
                // where the value was and remains ignored. What is deliberately
                // not new behaviour is [b=1] or a bare [x=y] in prose becoming
                // markup: text already stored would start rendering
                // differently, and an unquoted value on a tag that has no use
                // for one is far more likely to be text than markup.
                var bareOnTagWithoutAttribute =
                    bare.Success && matchingTag != null && !matchingTag.WithAttribute;

                if (matchingTag == null || bareOnTagWithoutAttribute)
                {
                    result.Text = input.Substring(position, match.Index + match.Length - position);
                    result.TagType = TagType.NoResult;
                }
                else
                {
                    result.Match = match.Value;
                    result.Text = input.Substring(position, match.Index - position);
                    result.Tag = matchingTag;
                    result.AttributeValue = bare.Success ? bare.Value : match.Groups["quoted"].Value;
                    result.TagType = match.Groups["closing"].Success ? TagType.Close : TagType.Open;
                }

                position = match.Index + match.Length;
                match = match.NextMatch();
                return true;
            }

            result.Text = input.Substring(position);
            position = input.Length;
            result.TagType = TagType.NoResult;
            return true;
        }
    }
}
