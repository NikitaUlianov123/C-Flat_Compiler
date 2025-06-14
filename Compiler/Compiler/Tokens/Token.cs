using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    [DebuggerDisplay("{Classification}: {Text}")]
    public class Token
    {
        public Definition.Classification Classification { get; set; }
        public string Regex { get; set; }//In case I need to know what exactly matched
        public string Text { get; set; }

        public Token(Definition.Classification classification, string regex, string text, params string[] captures)
        {
            Classification = classification;
            Regex = regex;
            if (captures.Length > 0)
            {
                Text = captures[0];
            }
            else
            {
                Text = text;
            }
        }
    }
}
