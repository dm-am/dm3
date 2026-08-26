// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using BBCodeParser.Nodes;
using BBCodeParser.Tags;

namespace BBCodeParser
{
    public interface IBbParser
    {
        NodeTree Parse(string input);
        Tag[] GetTags();
    }
}
