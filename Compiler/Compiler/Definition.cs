using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compiler
{
    public class Definition
    {
        public enum Classification
        {
            Whitespace,
            Keyword,
            Punctuation,
            Operator,
            Value,
            Identifier,
        }
        public static Dictionary<Classification, string[]> Regex = new()
        {
            [Classification.Whitespace] =
            [
                @"^[\s]+",
            ],
            [Classification.Keyword] =
            [
                @"^print\b",
            ],
            [Classification.Punctuation] =
            [
                @"^\(",
                @"^\)",
                @"^;",
                @"^,",
            ],
            [Classification.Operator] =
            [
                @"^=",
                @"^=\?",
            ],
            [Classification.Value] =
            [
                @"^""(?:[^""\\]|\\.)*""",
                @"^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?",
            ],
            [Classification.Identifier] =
            [
                @"^(^[A-Za-z]+)\b",
            ],
        };
    }
}