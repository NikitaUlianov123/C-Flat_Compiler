using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    internal class Whitespace : IToken
    {
        List<string> IToken.Regex => Regex;

        public static List<string> Regex =>
        [
            @"^[\s]+",
        ];

        public string Match { get; private set; }

        public Whitespace(string match)
        {
            Match = match;
        }

        public static bool DoesMatch(string input, [NotNullWhen(true)] out IToken? result)
        {
            foreach (var regex in Regex)
            {
                var match = System.Text.RegularExpressions.Regex.Match(input, regex);

                if (match.Success)
                {
                    result = new Whitespace(match.Groups[0].Value);
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
