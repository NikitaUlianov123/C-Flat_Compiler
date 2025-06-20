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
                                      [typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator), typeof(MathExpr), typeof(Semicolon)],//int a = 5;
                                      [typeof(Identifier), typeof(AssignmentOperator), typeof(MathExpr), typeof(Semicolon)]],//a = 2;

            [typeof(MathExpr)] = [[typeof(Identifier)],
                                  [typeof(NumericValue)]],
        };

        public static List<string> Parse(List<IToken> tokens, out ParseNode? root)
        {
            List<string> messages = [];
            root = new ParseNode();

            while (tokens.Count > 0)
            {
                var newKid = parse(typeof(Program), tokens, messages);

                if (newKid == null)//there was a problem
                {
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
            

            ParseNode? parse(Type nonTerminal, List<IToken> tokens, List<string> messages)
            {
                if (ParseNodes.ContainsKey(nonTerminal))
                {
                    foreach (var production in ParseNodes[nonTerminal])
                    {
                        if (tokens.Count < production.Count) continue;

                        ParseNode result = (ParseNode)Activator.CreateInstance(nonTerminal)!;

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
                                var newKid = parse(production[i], tempList, messages);
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

                if (nonTerminal is IToken)
                {
                    messages.Add("non-terminal is actually terminal, wtf?");
                }
                else
                {
                    messages.Add($"No productions for {nonTerminal.Name}. row {tokens[0].Row}, column {tokens[0].Column}");
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
    public record class MathExpr : ParseNode;
    public record class VariableExpr : ParseNode;
    public record class PrintStatement : ParseNode;
}