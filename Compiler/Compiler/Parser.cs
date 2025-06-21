using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Compiler.Tokens;

namespace Compiler
{
    public static class Parser
    {
        private static readonly Dictionary<Type, List<List<Type>>> ParseNodes = new()
        {
            [typeof(Program)] = [[typeof(PrintStatement)],
                                 [typeof(VariableExpr)]],
            [typeof(PrintStatement)] = [[typeof(PrintKeyword), typeof(OpenParenthesis), typeof(StringValue), typeof(CloseParenthesis), typeof(Semicolon)]],//print("hello")

            [typeof(VariableExpr)] = [[typeof(Identifier), typeof(Identifier), typeof(Semicolon)],//int a;
                                      [typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator), typeof(VariableValue), typeof(Semicolon)],//int a = 5;
                                      [typeof(Identifier), typeof(AssignmentOperator), typeof(VariableValue), typeof(Semicolon)]],//a = 2;
            [typeof(VariableValue)] = [[typeof(MathExpr)],
                                       [typeof(StringValue)]],

            #region Maph
            [typeof(MathExpr)] = [[typeof(MathTerm), typeof(MathExprTail)]],
            [typeof(MathExprTail)] = [[typeof(PlusOperator), typeof(MathTerm), typeof(MathExprTail)],
                                      [typeof(MinusOperator), typeof(MathTerm), typeof(MathExprTail)],
                                      []],

            [typeof(MathTerm)] = [[typeof(MathFactor), typeof(MathTermTail)]],
            [typeof(MathTermTail)] = [[typeof(TimesOperator), typeof(MathFactor), typeof(MathTermTail)],
                                      [typeof(DivideOperator), typeof(MathFactor), typeof(MathTermTail)],
                                      []],

            [typeof(MathFactor)] = [[typeof(OpenParenthesis), typeof(MathExpr), typeof(CloseParenthesis)],
                                    [typeof(NumericValue)],
                                    [typeof(Identifier)]],
            #endregion

            [typeof(IfStatement)] = [[typeof(IfKeyword), typeof(OpenParenthesis), typeof(BoolExpr), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)]],
            #region bool
            [typeof(BoolExpr)] = [[typeof(BoolAndExpr), typeof(BoolOrExprTail)]],
            [typeof(BoolOrExprTail)] = [[typeof(OrOperator), typeof(BoolAndExpr), typeof(BoolAndExprTail)],
                                        []],
            [typeof(BoolAndExpr)] = [[typeof(BoolFactor), typeof(BoolAndExpr)]],
            [typeof(BoolAndExprTail)] = [[typeof(AndOperator), typeof(BoolFactor), typeof(BoolAndExpr)],
                                         []],
            [typeof(BoolFactor)] = [[typeof(NotOperator), typeof(BoolFactor)],
                                    [typeof(OpenParenthesis), typeof(BoolExpr), typeof(CloseParenthesis)],
                                    [typeof(Comparison)],
                                    [typeof(BoolLiteral)],
                                    [typeof(Identifier)]],
            [typeof(Comparison)] = [[typeof(MathExpr), typeof(BoolRelativeOp), typeof(MathExpr)]],
            [typeof(BoolRelativeOp)] = [[typeof(LessThanOperator)],
                                        [typeof(LessThanOrEqualOperator)],
                                        [typeof(GreaterThanOperator)],
                                        [typeof(GreaterThanOrEqualOperator)],
                                        [typeof(EqualityOperator)],
                                        [typeof(NotEqualityOperator)]],
            [typeof(BoolLiteral)] = [[typeof(TrueKeyword)],
                                     [typeof(FalseKeyword)]],
            #endregion
        };

        public static List<string> Parse(List<IToken> tokens, out ParseNode? root)
        {
            List<string> messages = [];
            root = new ParseNode();

            while (tokens.Count > 0)
            {
                var newKid = parse(typeof(Program), tokens);

                if (newKid == null)//there was a problem
                {
                    messages.Add($"Invalid statement. row {tokens[0].Row}, column {tokens[0].Column}");

                    while (tokens[0] is not Semicolon)//panic mode
                    {
                        tokens.RemoveAt(0);
                    }
                    tokens.RemoveAt(0);
                }
                else
                { 
                    root.Children.Add(newKid);
                }
            }
            return messages;
            

            ParseNode? parse(Type nonTerminal, List<IToken> tokens)
            {
                if (ParseNodes.ContainsKey(nonTerminal))
                {
                    foreach (var production in ParseNodes[nonTerminal])
                    {
                        if (tokens.Count < production.Count) continue;

                        ParseNode result = (ParseNode)Activator.CreateInstance(nonTerminal)!;

                        if (production.Count == 0) //epsilon production
                        {
                            return result;
                        }

                        List<IToken> tempList = tokens.Select(x => x).ToList();//List.Copy yo!

                        bool success = true;
                        for (int i = 0; i < production.Count; i++)
                        {
                            if (typeof(IToken).IsAssignableFrom(production[i]))//child is IToken(terminal)
                            {
                                if (tempList[0].GetType() == production[i])//and the next token is what we want
                                {
                                    result.Children.Add(tempList[0]);
                                    tempList.RemoveAt(0);
                                }
                                else
                                {
                                    success = false;
                                    break;
                                }
                            }
                            else
                            {
                                var newKid = parse(production[i], tempList);
                                if (newKid == null)
                                { 
                                    success = false;
                                    break;
                                }
                                result.Children.Add(newKid);
                            }
                        }

                        if (success)
                        {
                            while (tokens.Count > tempList.Count)
                            { 
                                tokens.RemoveAt(0);
                            }
                            return result;
                        }
                    }
                }
                return null;
            }
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


    //[DebuggerDisplay("{this.GetType().Name}")]
    public record class ParseNode
    {
        public List<object> Children = [];//i know this is bad, it's (hopefully) temporary
    }


    public record class Program() : ParseNode;
    public record class PrintStatement : ParseNode;
    public record class VariableExpr : ParseNode;
    public record class VariableValue : ParseNode;
    #region Maph
    public record class MathExpr : ParseNode;
    public record class MathExprTail : ParseNode;
    public record class MathTerm : ParseNode;
    public record class MathTermTail : ParseNode;
    public record class MathFactor : ParseNode;
    #endregion

    public record class IfStatement : ParseNode;
    #region bool
    public record class BoolExpr : ParseNode;
    public record class BoolOrExprTail : ParseNode;
    public record class BoolAndExpr : ParseNode;
    public record class BoolAndExprTail : ParseNode;
    public record class BoolRelativeOp : ParseNode;
    public record class BoolFactor : ParseNode;
    public record class BoolLiteral : ParseNode;
    public record class Comparison : ParseNode;
    #endregion
}