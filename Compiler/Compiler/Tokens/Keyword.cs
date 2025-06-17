using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler
{
    internal class Keyword : Token
    {
        public string Word { get; set; }

        public static List<string> Regex =
        [
            @"^(print)\b",
            @"^(if)\b",
            @"^(else)\b",
        ];

        public Keyword(Match match)
            : base(match.Groups[0].Value)
        {
            Word = match.Groups[1].Value;
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
