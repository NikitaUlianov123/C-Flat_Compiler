using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    internal class Operator : IToken
    {
        public enum OperatorType
        {
            AssignmentEqual,
            CloseParen,
            SemiColon,
            Comma
        }

        public string Word { get; private set; }

        public string Match { get; private set; }

        List<string> IToken.Regex => Regex;

        public static List<string> Regex =
        [
            @"^=",
            @"^=\?",
        ];

        public Operator(Match match)
        {
            Match = match.Groups[0].Value;
        }

        public static bool DoesMatch(string input, [NotNullWhen(true)] out IToken? result)
        {
            foreach (var regex in Regex)
            {
                var match = System.Text.RegularExpressions.Regex.Match(input, regex);

                if (match.Success)
                {
                    result = new Keyword(match);
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
