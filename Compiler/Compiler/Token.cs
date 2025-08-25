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
        string Text { get; init; }
        int Row { get; init; }
        int Column { get; init; }
    }

    public static class TokenFactory
    {
        private static readonly Dictionary<string, Func<string, int, int, IToken>> TokenTypes = new()
        {
            // Whitespace
            [@"^[\s]+"] = (text, row, column) => new WhiteSpace(text, row, column),

            // Keywords
            [@"^print\b"] = (text, row, column) => new PrintKeyword(text, row, column),
            [@"^if\b"] = (text, row, column) => new IfKeyword(text, row, column),
            [@"^ifn't\b"] = (text, row, column) => new IfKeyword(text, row, column),
            [@"^else\b"] = (text, row, column) => new ElseKeyword(text, row, column),
            [@"^while\b"] = (text, row, column) => new WhileKeyword(text, row, column),
            [@"^for\b"] = (text, row, column) => new ForKeyword(text, row, column),
            [@"^true\b"] = (text, row, column) => new TrueKeyword(text, row, column),
            [@"^false\b"] = (text, row, column) => new FalseKeyword(text, row, column),

            // Punctuation
            [@"^\("] = (text, row, column) => new OpenParenthesis(text, row, column),
            [@"^\)"] = (text, row, column) => new CloseParenthesis(text, row, column),
            [@"^;"] = (text, row, column) => new Semicolon(text, row, column),
            [@"^,"] = (text, row, column) => new Comma(text, row, column),
            [@"^\{"] = (text, row, column) => new OpenCurlyBracket(text, row, column),
            [@"^\}"] = (text, row, column) => new CloseCurlyBracket(text, row, column),

            // Operators
            [@"^=\?"] = (text, row, column) => new EqualityOperator(text, row, column),
            [@"^!="] = (text, row, column) => new NotEqualityOperator(text, row, column),
            [@"^="] = (text, row, column) => new AssignmentOperator(text, row, column),
            // Math Operators
            [@"^\+"] = (text, row, column) => new PlusOperator(text, row, column),
            [@"^-"] = (text, row, column) => new MinusOperator(text, row, column),
            [@"^\*"] = (text, row, column) => new TimesOperator(text, row, column),
            [@"^/"] = (text, row, column) => new DivideOperator(text, row, column),
            //Bool operators
            [@"^&&"] = (text, row, column) => new AndOperator(text, row, column),
            [@"^\|\|"] = (text, row, column) => new OrOperator(text, row, column),
            [@"^!"] = (text, row, column) => new NotOperator(text, row, column),
            [@"^>="] = (text, row, column) => new GreaterThanOrEqualOperator(text, row, column),
            [@"^<="] = (text, row, column) => new LessThanOrEqualOperator(text, row, column),
            [@"^>"] = (text, row, column) => new GreaterThanOperator(text, row, column),
            [@"^<"] = (text, row, column) => new LessThanOperator(text, row, column),

            // Values
            [@"^""(?:[^""\\]|\\.)*"""] = (text, row, column) => new StringValue(text, row, column),
            [@"^[+-]?\d+(\.\d+)?([eE][+-]?\d+)?"] = (text, row, column) => new NumericValue(text, row, column),

            // Identifiers
            [@"^[A-Za-z_][A-Za-z0-9_]*\b"] = (text, row, column) => new Identifier(text, row, column),
        };

        public static IToken MakeToken(string input, int row, int column)
        {
            foreach (var type in TokenTypes)
            {
                var match = Regex.Match(input, type.Key);

                if (match.Success)
                {
                    return type.Value(match.Groups[0].Value, row, column);
                }
            }

            return new Error(input[0].ToString(), row, column);
        }
    }

    public record WhiteSpace(string Text, int Row, int Column) : IToken;

    #region Keywords
    public record PrintKeyword(string Text, int Row, int Column) : IToken;
    public record IfKeyword(string Text, int Row, int Column) : IToken;
    public record IfntKeyword(string Text, int Row, int Column) : IToken;
    public record ElseKeyword(string Text, int Row, int Column) : IToken;
    public record WhileKeyword(string Text, int Row, int Column) : IToken;
    public record ForKeyword(string Text, int Row, int Column) : IToken;
    public record TrueKeyword(string Text, int Row, int Column) : IToken;
    public record FalseKeyword(string Text, int Row, int Column) : IToken;
    #endregion

    #region Punctuation
    public record OpenParenthesis(string Text, int Row, int Column) : IToken;
    public record CloseParenthesis(string Text, int Row, int Column) : IToken;
    public record Semicolon(string Text, int Row, int Column) : IToken;
    public record Comma(string Text, int Row, int Column) : IToken;
    public record OpenCurlyBracket(string Text, int Row, int Column) : IToken;
    public record CloseCurlyBracket(string Text, int Row, int Column) : IToken;
    #endregion

    #region Operators
    public record AssignmentOperator(string Text, int Row, int Column) : IToken;

    #region Math Operators
    public record PlusOperator(string Text, int Row, int Column) : IToken;
    public record MinusOperator(string Text, int Row, int Column) : IToken;
    public record TimesOperator(string Text, int Row, int Column) : IToken;
    public record DivideOperator(string Text, int Row, int Column) : IToken;

    #endregion

    #region Bool Operators
    public record EqualityOperator(string Text, int Row, int Column) : IToken;
    public record NotEqualityOperator(string Text, int Row, int Column) : IToken;
    public record AndOperator(string Text, int Row, int Column) : IToken;
    public record OrOperator(string Text, int Row, int Column) : IToken;
    public record NotOperator(string Text, int Row, int Column) : IToken;
    public record GreaterThanOperator(string Text, int Row, int Column) : IToken;
    public record LessThanOperator(string Text, int Row, int Column) : IToken;
    public record GreaterThanOrEqualOperator(string Text, int Row, int Column) : IToken;
    public record LessThanOrEqualOperator(string Text, int Row, int Column) : IToken;
    #endregion
    #endregion

    #region Values
    public record StringValue(string Text, int Row, int Column) : IToken;
    [DebuggerDisplay("Numeric value: {Text}")]
    public record NumericValue(string Text, int Row, int Column) : IToken
    {
        public int Number => int.Parse(Text);
    }
    #endregion

    public record Identifier(string Text, int Row, int Column) : IToken;

    public record Error(string Text, int Row, int Column) : IToken;
}
