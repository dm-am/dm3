// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

namespace BBCodeParser.Tags
{
    // DM3 note: a block shown as written, nothing inside it markup. The name is
    // the original's; the one such block is [noparse], not [code].
    public class CodeTag : Tag
    {
        public CodeTag(string name, string openTag, string closeTag) : base(name, openTag, closeTag)
        {
        }
    }
}
