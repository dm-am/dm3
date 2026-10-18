// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

namespace BBCodeParser.Tags
{
    // DM3 note: a code tag is a block "shown as written". Nothing between its
    // opening tag and its own closing tag is read as markup, and a block the
    // author never closed is not one: see BbParser.Parse and TagNode.
    //
    // The name is the original's. The catalogue declares one such block,
    // [noparse]; [code] is not one of them, because on DM2 it changes the font
    // and nothing else, and the markup inside it works as it does anywhere.
    public class CodeTag : Tag
    {
        public CodeTag(string name, string openTag, string closeTag) : base(name, openTag, closeTag)
        {
        }
    }
}
