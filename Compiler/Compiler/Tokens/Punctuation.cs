using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    internal class Punctuation : IToken
    {
        public enum PunctuationType
        { 
            OpenParen,
            CloseParen,
            SemiColon,
            Comma
        }

        public PunctuationType Type { get; private set; }

        public string Match { get; private set; }

        List<string> IToken.Regex => Regex;

        public static List<string> Regex =
        [
            @"^\(",
            @"^\)",
            @"^;",
            @"^,",
        ];

        public Punctuation(Match match, PunctuationType type)
        {
            Match = match.Groups[0].Value;
            Type = type;
        }

        public static bool DoesMatch(string input, [NotNullWhen(true)] out IToken? result)
        {
            for (int i = 0; i < Regex.Count; i++)
            {
                var match = System.Text.RegularExpressions.Regex.Match(input, Regex[i]);

                if (match.Success)
                {
                    result = new Punctuation(match, (PunctuationType)i);
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
