using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    internal class Error : IToken
    {
        public string Match { get; private set; }

        public Error(string match)
        {
            Match = match;
        }

        public void Append(string input)
            => Match += input;
    }
}
