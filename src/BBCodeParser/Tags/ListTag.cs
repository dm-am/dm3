// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

namespace BBCodeParser.Tags
{
    public class ListTag : Tag
    {
        public ListTag(string name, string openTag, string closeTag, bool withAttribute = false, bool secure = true)
            : base(name, openTag, closeTag, withAttribute, secure)
        {
        }

        public ListTag(string name, string openTag, bool withAttribute = false, bool secure = true)
            : base(name, openTag, withAttribute, secure)
        {
        }
    }
}
