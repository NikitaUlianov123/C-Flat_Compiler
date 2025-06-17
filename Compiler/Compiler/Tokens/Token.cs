using Compiler.Tokens;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler
{
    [DebuggerDisplay("{Match}")]
    public abstract class Token
    {
        //protected abstract List<string> Regex { get; }

        public string Match { get; set; }//In case I need to know what exactly matched


        public Token(string match)
        {
            Match = match;
        }

        public static Token? MakeToken(string input)
        {
            var match = Whitespace.DoesMatch(input);
            if (match != null)
            {
                return new Whitespace(match);
            }

            match = Keyword.DoesMatch(input);
            if (match != null)
            {
                return new Keyword(match);
            }




            return null;
        }
    }
}
