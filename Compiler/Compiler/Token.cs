using Compiler.Tokens;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    public interface IToken
    {
        public string Text { get; init; }
    }

    public static class TokenFactory
    {
        private static readonly Dictionary<string, Func<string, IToken>> TokenTypes = new()
        {
            // Whitespace
            [@"^[\s]+"] = (text) => new WhiteSpace(text),

            // Keywords
            [@"^print\b"] = (text) => new PrintKeyword(text),
            [@"^if\b"] = (text) => new IfKeyword(text),
            [@"^else\b"] = (text) => new ElseKeyword(text),

            // Punctuation
            [@"^\("] = (text) => new OpenParenthesis(text),
            [@"^\)"] = (text) => new CloseParenthesis(text),
            [@"^;"] = (text) => new Semicolon(text),
            [@"^,"] = (text) => new Comma(text),

            // Operators
            [@"^="] = (text) => new AssignmentOperator(text),
            [@"^=\?"] = (text) => new EqualityOperator(text),

            // Values
            [@"^""(?:[^""\\]|\\.)*"""] = (text) => new StringValue(text),
            [@"^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?"] = (text) => new NumericValue(text),

            // Identifiers
            [@"^[A-Za-z_][A-Za-z0-9_]*\b"] = (text) => new Identifier(text),
        };

        public static IToken MakeToken(string input)
        {
            foreach (var type in TokenTypes)
            {
                var match = Regex.Match(input, type.Key);

                if (match.Success)
                {
                    return type.Value(match.Groups[0].Value);
                }
            }

            return new Error(input[0].ToString());
        }
    }

    public record WhiteSpace(string Text) : IToken;

    #region Keywords
    public record PrintKeyword(string Text) : IToken;
    public record IfKeyword(string Text) : IToken;
    public record ElseKeyword(string Text) : IToken;
    #endregion

    #region Punctuation
    public record OpenParenthesis(string Text) : IToken;
    public record CloseParenthesis(string Text) : IToken;
    public record Semicolon(string Text) : IToken;
    public record Comma(string Text) : IToken;
    #endregion

    #region Operators
    public record AssignmentOperator(string Text) : IToken;
    public record EqualityOperator(string Text) : IToken;
    #endregion

    #region Values
    public record StringValue(string Text) : IToken;
    public record NumericValue(string Text) : IToken;
    #endregion

    public record Identifier(string Text) : IToken;

    public record Error(string Text) : IToken;
}
