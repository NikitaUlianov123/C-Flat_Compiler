using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Compiler.Tokens;

namespace Compiler
{
    public static class Parser
    {
        private static readonly Dictionary<IToken, List<int>> ParseNodes = new()
        {
            //[WhiteSpace] = [0],
        };

        public static void Parse(List<IToken> tokens)
        {

        }

        public static void NukeWhiteSpace(List<IToken> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] is WhiteSpace)
                {
                    tokens.RemoveAt(i);
                    i--;
                }
            }
        }
    }

    public abstract class ParseNode
    {
        public List<ParseNode> Tokens = [];
        //public static virtual bool DoesParse(List<IToken> tokens, int index, out int newIndex, List<string> messages, [NotNullWhen(true)] out ParseNode? parseNode);
    }

    public class Terminal : ParseNode
    {
        public IToken Token { get; private set; }

        public Terminal(IToken token)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
        }
    }

    public class Program : ParseNode
    {
        public Program(List<ParseNode> tokens)
        {
            Tokens = tokens;
        }

        public static bool DoesParse(List<IToken> tokens, int index, out int newIndex, List<string> messages, [NotNullWhen(true)] out ParseNode? parseNode)
        {
            void missingMessage(char character, int tokenIndex)
            { 
                messages.Add($"Missing '{character}'. row:{tokens[index + tokenIndex].Row}, column:{index + tokens[tokenIndex].Column}.");
            }
            void message(string message, int tokenIndex)
            {
                messages.Add($"{message}. row:{tokens[tokenIndex].Row}, column:{tokens[tokenIndex].Column}.");
            }

            //print
            if (tokens[index] is PrintKeyword && tokens.Count > index + 4)
            {
                //(
                if (tokens[index + 1] is OpenParenthesis)
                {
                    //string
                    if (tokens[index + 2] is StringValue)
                    {
                        //)
                        if (tokens[index + 3] is CloseParenthesis)
                        {
                            //;
                            if (tokens[index + 4] is Semicolon)
                            {
                                newIndex = index + 5;
                                parseNode = new Program([new Terminal(tokens[index]),
                                                         new Terminal(tokens[index + 1]),
                                                         new Terminal(tokens[index + 2]),
                                                         new Terminal(tokens[index + 3]),
                                                         new Terminal(tokens[index + 4])]);
                                return true;
                            }
                            else missingMessage(';', 3);
                        }
                        else missingMessage(')', 2);
                    }
                    else message("Invalid value, not a string literal.", 1);
                }
                else missingMessage('(', 0);
            }
            if (tokens[index] is IfKeyword)
            {
                throw new NotImplementedException("If statements are not yet implemented.");
            }
            if (tokens[index] is ElseKeyword)
            {
                throw new NotImplementedException("Else statements are not yet implemented.");
            }
            if (tokens[index] is Identifier && tokens.Count > index + 3)
            {
                //variable declaration
                if (tokens[index + 1] is Identifier)
                {
                    //int a = something;
                    if (tokens[index + 2] is AssignmentOperator)
                    {
                        if (MathExpr.DoesMatch(tokens, index + 3, out int tempIndex, messages, out MathExpr? result))
                        {

                        }
                        else
                        { 
                            
                            return false;
                        }
                    }

                    //int a;
                    if (tokens[index + 2] is Semicolon)
                    {
                        newIndex = index + 3;
                        parseNode = new Program([new Terminal(tokens[index]),
                                                 new Terminal(tokens[index + 1]),
                                                 new Terminal(tokens[index + 2])]);
                        return true;
                    }
                    else missingMessage(';', 2);
                }
                //variable assignment
                if (tokens[index + 1] is AssignmentOperator)
                {
                    throw new NotImplementedException("can't assign variables yet");
                }
            }
            else //nothing matched
            {
                message("Invalid start of line", 0);
                newIndex = 0;
                parseNode = null;
                return false;
            }
        }
    }

    public class MathExpr : ParseNode
    {
        public MathExpr(List<ParseNode> tokens)
        {
            Tokens = tokens;
        }

        public static bool DoesMatch(List<IToken> tokens, int index, out int newIndex, List<string> messages, [NotNullWhen(true)] out MathExpr? parseNode)
        {
            if (tokens[index] is NumericValue or Identifier)
            {
                newIndex = index + 1;
                parseNode = new MathExpr([new Terminal(tokens[index])]);
                return true;
            }
            else
            {
                messages.Add($"Invalid expression. row:{tokens[index].Row}, column:{tokens[index].Column}.");
                newIndex = 0;
                parseNode = null;
                return false;
            }
        }
    }
}