// Vendored from https://github.com/quilin/BBCodeParser - MIT, see LICENSE in
// this directory for the original copyright. The sources are the state that
// package BBCodeParser 1.0.0 was built from: upstream commit
// c2ccefdf9d657e130186b60fc253821c7af4e168.

using System;

namespace BBCodeParser
{
    public class BbParserException : Exception
    {
        public BbParserException() : base("Tree is too deep")
        {
        }
    }
}
