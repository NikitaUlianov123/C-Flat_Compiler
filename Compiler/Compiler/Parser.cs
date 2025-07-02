using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

using Compiler.Tokens;

namespace Compiler
{
    public static class Parser
    {
        private static readonly Dictionary<Type, List<List<Type>>> ParseNodes = new()
        {
            [typeof(Program)] = [[typeof(PossibleStatements), typeof(Program)],
                                 []],
            [typeof(PossibleStatements)] = [
                                            [typeof(PrintStatement)],
                                            [typeof(VariableExpr)],
                                            [typeof(IfStatement)],
                                            [typeof(WhileLoop)],
                                            ],

            [typeof(PrintStatement)] = [[typeof(PrintKeyword), typeof(OpenParenthesis), typeof(StringValue), typeof(CloseParenthesis), typeof(Semicolon)],//print("hello")
                                        [typeof(PrintKeyword), typeof(OpenParenthesis), typeof(Identifier), typeof(CloseParenthesis), typeof(Semicolon)]],//print(a);

            [typeof(VariableExpr)] = [[typeof(VariableDeclaration)],
                                      [typeof(VariableAssignment)]],
            [typeof(VariableDeclaration)] = [[typeof(Identifier), typeof(Identifier), typeof(Semicolon)],//int a;
                                             [typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator), typeof(VariableValue), typeof(Semicolon)]],//int a = 5;
            [typeof(VariableAssignment)] = [[typeof(Identifier), typeof(AssignmentOperator), typeof(VariableValue), typeof(Semicolon)]],
            [typeof(VariableValue)] = [[typeof(MathExpr)],
                                       [typeof(StringValue)],
                                       [typeof(BoolLiteral)]],

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
            [typeof(WhileLoop)] = [[typeof(WhileKeyword), typeof(OpenParenthesis), typeof(BoolExpr), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)]],
            #region bool
            [typeof(BoolExpr)] = [[typeof(BoolAndExpr), typeof(BoolOrExprTail)]],
            [typeof(BoolOrExprTail)] = [[typeof(OrOperator), typeof(BoolAndExpr), typeof(BoolOrExprTail)],
                                        []],
            [typeof(BoolAndExpr)] = [[typeof(BoolFactor), typeof(BoolAndExprTail)]],
            [typeof(BoolAndExprTail)] = [[typeof(AndOperator), typeof(BoolFactor), typeof(BoolAndExprTail)],
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
            NukeWhiteSpace(tokens);

            List<string> messages = [];
            root = new ParseNode();

            while (tokens.Count > 0)
            {
                var newKid = parse(typeof(Program), tokens);

                if (newKid == null || newKid.Children.Count == 0)//there was a problem
                {
                    messages.Add($"Invalid statement. row {tokens[0].Row}, column {tokens[0].Column}");

                    while (tokens[0] is not Semicolon && tokens.Count > 1)//panic mode
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
            root = MakeAST(root);
            root = root.Hoist();

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

        private static ParseNode MakeAST(ParseNode root)
        {
            root.Collapse();
            return root;
        }

        private static void NukeWhiteSpace(List<IToken> tokens)
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
    public class OpensScopeAttribute : Attribute;

    public record class ParseNode
    {
        public List<object> Children = [];//i know this is bad, it's (hopefully) temporary
        public virtual (int row, int column) Location { get; protected set; }
        public string TypeExpected { get; set; } = "";
        public virtual bool IsColapsable
        {
            get
            {
                int nonCollapsableChildren = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] is IToken || (Children[i] is ParseNode child && !child.IsColapsable))
                    {
                        nonCollapsableChildren++;
                    }
                }

                return nonCollapsableChildren <= 1;
            }
        }

        public ParseNode? Collapse()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ParseNode child)
                {
                    var newChild = child.Collapse();
                    if (newChild is null)
                    {
                        Children.RemoveAt(i--);
                        continue;
                    }
                    Children[i] = newChild;
                }
            }
            if (IsColapsable)
            {
                if (Children.Count == 0) return null;

                var child = Children.Where(x => x is IToken || (x is ParseNode child && !child.IsColapsable)).First();
                if (child is IToken token)
                {
                    return new ASTNode(token);
                }
                else if (child is ParseNode parseNode)
                {
                    return parseNode;
                }
            }

            return this;
        }

        public virtual ParseNode Hoist()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ParseNode child)
                {
                    var newChild = child.Hoist();
                    if (newChild is null)
                    {
                        Children.RemoveAt(i--);
                        continue;
                    }
                    Children[i] = newChild;
                }
            }

            return this;
        }
    }

    [DebuggerDisplay("Token: {Token}")]
    public record class ASTNode(IToken Token) : ParseNode
    {
        public override bool IsColapsable => false;
        public override (int row, int column) Location => (Token.Row, Token.Column);

        public ASTNode(IToken token, string type = "") : this(token)
        {
            TypeExpected = type;
        }

        public override string ToString()
        {
            return Token.Text;
        }
    }

    public record class Program() : ParseNode;
    public record class PossibleStatements : ParseNode;
    
    public record class PrintStatement : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            var result = new ASTNode((Children[0] as IToken)!);//the print keyword

            //the stuff between the parens
            if (Children[2] is StringValue)
            {
                result.Children.Add(new ASTNode((Children[2] as IToken)!, "string"));
            }
            else
            { 
                result.Children.Add(new ASTNode((Children[2] as IToken)!));
            }
            return result;
        }
    }
    public record class VariableExpr : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            return (Children[0] as ParseNode)!;
        }
    }
    public record class VariableAssignment : ParseNode
    {
        public string Name { get; private set; } = "";
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Name = (Children[0] as IToken)!.Text;
            Children.RemoveAt(0);//remove the name token

            Children.RemoveAt(0);//remove the assignment operator

            Children.RemoveAt(Children.Count - 1);//remove the semicolon

            return this;
        }
    }
    public record class VariableDeclaration : ParseNode
    {
        public string Type { get; private set; } = "";
        public string Name { get; private set; } = "";
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Type = (Children[0] as IToken)!.Text;
            Children.RemoveAt(0);//remove the type token

            Name = (Children[0] as IToken)!.Text;
            Children.RemoveAt(0);//remove the name token

            if (Children.Count > 1)//if there is an assignment(more than just ;)
            {
                Children.RemoveAt(0);//remove the assignment operator
            }

            Children.RemoveAt(Children.Count - 1);//remove the semicolon

            return this;
        }
    }
    public record class VariableValue : ParseNode;

    #region Maph
    public record class MathExpr : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            ParseNode result;
            if (Children[1] is IToken token)
            {
                result = new ASTNode(token);
            }
            else
            {
                result = (Children[1] as ParseNode)!;
            }
            
            result.Children.Insert(0, Children[0]);
            
            return result;
        }
    }
    public record class MathExprTail : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            var result = new ASTNode((Children[0] as IToken)!);//the operator

            result.Children.Add(Children[1]);

            if (Children.Count > 2)
            {
                result.Children.Add(Children[2]);
            }
            return result;
        }
    }
    public record class MathTerm : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            ParseNode result;
            if (Children[1] is IToken token)
            {
                result = new ASTNode(token);
            }
            else
            {
                result = (Children[1] as ParseNode)!;
            }

            result.Children.Insert(0, Children[0]);

            return result;
        }
    }
    public record class MathTermTail : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            var result = new ASTNode((Children[0] as IToken)!);//the operator

            result.Children.Add(Children[1]);

            if (Children.Count > 2)
            {
                result.Children.Add(Children[2]);
            }
            return result;
        }
    }
    public record class MathFactor : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children.Count == 3)
            {
                return (Children[1] as ParseNode)!;
            }

            return this;
        }
    }
    #endregion

    [OpensScope]
    public record class IfStatement : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Children.RemoveAt(0); //remove the if keyword
            Children.RemoveAt(0); //remove the open parenthesis
            Children.RemoveAt(1); //remove the close parenthesis
            Children.RemoveAt(1); //remove the open curly bracket
            if (Children.Count > 2)//if there is a body
            {
                Children.RemoveAt(2); //remove the close curly bracket
            }
            else
            { 
                Children.RemoveAt(1); //remove the close curly bracket
            }

            (Children[0] as ParseNode)!.TypeExpected = "bool";//the condition should be a bool

            return this;
        }
    }
    [OpensScope]
    public record class WhileLoop : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Children.RemoveAt(0); //remove the while keyword
            Children.RemoveAt(0); //remove the open parenthesis
            Children.RemoveAt(1); //remove the close parenthesis
            Children.RemoveAt(1); //remove the open curly bracket
            if (Children.Count > 2)//if there is a body
            {
                Children.RemoveAt(2); //remove the close curly bracket
            }
            else
            {
                Children.RemoveAt(1); //remove the close curly bracket
            }

            (Children[0] as ParseNode)!.TypeExpected = "bool";//the condition should be a bool

            return this;
        }
    }
    #region bool
    public record class BoolExpr : ParseNode;
    public record class BoolOrExprTail : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            ParseNode result;//the operator
            if (Children[0] is IToken)
            {
                result = new ASTNode((Children[0] as IToken)!);
            }
            else
            {
                result = (Children[0] as ParseNode)!;
            }


            result.Children.AddRange(Children[1..]);

            return result;
        }
    }
    public record class BoolAndExpr : ParseNode;
    public record class BoolAndExprTail : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            ParseNode result;//the operator
            if (Children[0] is IToken)
            {
                result = new ASTNode((Children[0] as IToken)!);
            }
            else
            {
                result = (Children[0] as ParseNode)!;
            }


            result.Children.AddRange(Children[1..]);

            return result;
        }
    }
    public record class BoolRelativeOp : ParseNode;
    public record class BoolFactor : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children[0] is NotOperator || (Children[0] is ASTNode node && node.Token is NotOperator))
            { 
                var result = Children[0] as ParseNode ?? new ASTNode((Children[0] as NotOperator)!);

                result.Children.Add(Children[1]);//the stuff being notted
                return result;
            }

            if (Children[0] is OpenParenthesis || (Children[0] is ASTNode parenNode && parenNode.Token is OpenParenthesis))
            {
                return Children[1] as ParseNode ?? new ASTNode((Children[1] as OpenParenthesis)!);
            }

            return this;
        }
    }
    public record class BoolLiteral : ParseNode;
    public record class Comparison : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            ParseNode result;//the operator
            if (Children[1] is IToken)
            {
                result = new ASTNode((Children[1] as IToken)!);
            }
            else
            { 
                result = (Children[1] as ParseNode)!;
            }
            result.Children.Add(Children[0]);//the left side
            (result.Children[0] as ParseNode)!.TypeExpected = "int";

            result.Children.Add(Children[2]);//the right side
            (result.Children[1] as ParseNode)!.TypeExpected = "int";


            result.TypeExpected = "int";


            return result;
        }
    }
    #endregion
}