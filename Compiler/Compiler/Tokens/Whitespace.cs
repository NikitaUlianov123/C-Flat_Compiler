using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    internal class Whitespace : Token
    {
        public static List<string> Regex =>
        [
            @"^[\s]+",
        ];

        public Whitespace(Match match)
            : base(match.Groups[0].Value)
        {

        }

        public static Match? DoesMatch(string input)
        {
            foreach (var regex in Regex)
            {
                var match = System.Text.RegularExpressions.Regex.Match(input, regex);

                if (match.Success) return match;
            }

            return null;
        }
    }
}
