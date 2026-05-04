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
                                            [typeof(ClassDeclaration)],
                                            [typeof(PrintStatement), typeof(Semicolon)],
                                            [typeof(VariableExpr), typeof(Semicolon)],
                                            [typeof(IfStatement)],
                                            [typeof(IfntStatement)],
                                            [typeof(WhileLoop)],
                                            [typeof(ForLoop)],
                                            [typeof(Label)],
                                            [typeof(GotoStatement), typeof(Semicolon)],
                                            [typeof(FunctionDeclaration)],
                                            [typeof(FunctionCall), typeof(Semicolon)],
                                            [typeof(ReturnStatement), typeof(Semicolon)],
                                            ],

            [typeof(PrintStatement)] = [[typeof(PrintKeyword), typeof(OpenParenthesis), typeof(StringValue), typeof(CloseParenthesis)],//print("hello")
                                        [typeof(PrintKeyword), typeof(OpenParenthesis), typeof(VariableName), typeof(CloseParenthesis)]],//print(a)

            #region Variables
            [typeof(VariableExpr)] = [[typeof(VariableDeclarationAndAssignment)],
                                      [typeof(VariableDeclaration)],
                                      [typeof(VariableAssignment)]],
            [typeof(VariableDeclaration)] = [[typeof(Identifier), typeof(Identifier)]], //int a
            [typeof(VariableDeclarationAndAssignment)] = [[typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator), typeof(VariableValue)]],//int a = 5
            [typeof(VariableAssignment)] = [[typeof(VariableName), typeof(AssignmentOperator), typeof(VariableValue)],//a = 5
                                            [typeof(Incrementer)]],//a++ or --a
            [typeof(VariableValue)] = [[typeof(MathExpr)],
                                       [typeof(StringValue)],
                                       [typeof(BoolExpr)],
                                       [typeof(ClassInstantiation)]],

            [typeof(Incrementer)] = [[typeof(VariableName), typeof(IncrementOperator)],
                                     [typeof(VariableName), typeof(DecrementOperator)],
                                     [typeof(IncrementOperator), typeof(VariableName)],
                                     [typeof(DecrementOperator), typeof(VariableName)]],

            [typeof(VariableName)] = [[typeof(Identifier), typeof(Dot), typeof(VariableName)],
                                      [typeof(Identifier)]],
            #endregion

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
                                    [typeof(ExpressionIncrementer)],
                                    [typeof(FunctionCall)],
                                    [typeof(VariableName)]],

            [typeof(ExpressionIncrementer)] = [[typeof(VariableName), typeof(IncrementOperator)],//if you just say Incrementer, it will
                                               [typeof(VariableName), typeof(DecrementOperator)],//make every ExpressionIncrementer
                                               [typeof(IncrementOperator), typeof(VariableName)],//into an Incrementer during AST
                                               [typeof(DecrementOperator), typeof(VariableName)]],//generation
            #endregion

            #region Flow control
            [typeof(IfStatement)] = [[typeof(IfKeyword), typeof(OpenParenthesis), typeof(BoolExpr), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket), typeof(IfFollowUp)]],
            [typeof(IfntStatement)] = [[typeof(IfntKeyword), typeof(OpenParenthesis), typeof(BoolExpr), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket), typeof(IfFollowUp)]],
            [typeof(IfFollowUp)] = [[typeof(ElseKeyword), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)],
                                    [typeof(ElseKeyword), typeof(IfStatement)],
                                    [typeof(ElseKeyword), typeof(IfntStatement)],
                                    []],

            [typeof(WhileLoop)] = [[typeof(WhileKeyword), typeof(OpenParenthesis), typeof(BoolExpr), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket), typeof(WhileFollowUp)]],
            [typeof(WhileFollowUp)] = [[typeof(ElseKeyword), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)],
                                    []],

            [typeof(ForLoop)] = [[typeof(ForKeyword), typeof(OpenParenthesis), typeof(VariableDeclarationAndAssignment), typeof(Semicolon), typeof(BoolExpr), typeof(Semicolon), typeof(VariableAssignment), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)],
                                 [typeof(ForKeyword), typeof(OpenParenthesis), typeof(VariableAssignment), typeof(Semicolon), typeof(BoolExpr), typeof(Semicolon), typeof(VariableAssignment), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)]],

            [typeof(GotoStatement)] = [[typeof(GotoKeyword), typeof(Identifier)]],
            #endregion

            #region Functions
            [typeof(FunctionDeclaration)] = [[typeof(Identifier), typeof(Identifier), typeof(OpenParenthesis), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)],//void Name(){code}
                                             [typeof(Identifier), typeof(Identifier), typeof(OpenParenthesis), typeof(FunctionParameter), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)]],//void Name(params){code}

            [typeof(FunctionCall)] = [[typeof(Identifier), typeof(OpenParenthesis), typeof(CloseParenthesis)],//Name()
                                      [typeof(Identifier), typeof(OpenParenthesis), typeof(FunctionCallParameter), typeof(CloseParenthesis)],//Name(params)
                                      [typeof(Identifier), typeof(Dot), typeof(Identifier), typeof(OpenParenthesis), typeof(CloseParenthesis)],//Class.Name()
                                      [typeof(Identifier), typeof(Dot), typeof(Identifier), typeof(OpenParenthesis), typeof(FunctionCallParameter), typeof(CloseParenthesis)]],//Class.Name(params)

            [typeof(FunctionParameter)] = [[typeof(Identifier), typeof(Identifier), typeof(Comma), typeof(FunctionParameter)],
                                           [typeof(Identifier), typeof(Identifier)]],

            [typeof(FunctionCallParameter)] = [[typeof(VariableValue), typeof(Comma), typeof(FunctionCallParameter)],
                                               [typeof(VariableValue)]],

            [typeof(ReturnStatement)] = [[typeof(ReturnKeyword), typeof(VariableValue)],
                                         [typeof(ReturnKeyword)]],
            #endregion

            #region Classes
            [typeof(ClassDeclaration)] = [[typeof(ClassKeyword), typeof(Identifier), typeof(OpenCurlyBracket), typeof(ClassBody), typeof(CloseCurlyBracket)]],//class Name { members }

            [typeof(ClassBody)] = [[typeof(ClassMember), typeof(ClassBody)],
                                   []],

            [typeof(ClassMember)] = [[typeof(FunctionDeclaration)],
                                     [typeof(ConstructorDeclaration)],
                                     //[typeof(VariableDeclarationAndAssignment), typeof(Semicolon)],
                                     [typeof(VariableDeclaration), typeof(Semicolon)]],

            [typeof(ConstructorDeclaration)] = [[typeof(Identifier), typeof(OpenParenthesis), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)],//Cat(){code}
                                                [typeof(Identifier), typeof(OpenParenthesis), typeof(FunctionParameter), typeof(CloseParenthesis), typeof(OpenCurlyBracket), typeof(Program), typeof(CloseCurlyBracket)]],//Cat(params){code}

            [typeof(ClassInstantiation)] = [[typeof(NewKeyword), typeof(Identifier), typeof(OpenParenthesis), typeof(CloseParenthesis)],//new Cat()
                                            [typeof(NewKeyword), typeof(Identifier), typeof(OpenParenthesis), typeof(FunctionCallParameter), typeof(CloseParenthesis)]],//new Cat(params)
            #endregion

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
                                    [typeof(VariableName)]],
            [typeof(Comparison)] = [[typeof(MathExpr), typeof(BoolRelativeOp), typeof(MathExpr)]],
            [typeof(BoolRelativeOp)] = [[typeof(LessThanOperator)],
                                        [typeof(LessThanOrEqualOperator)],
                                        [typeof(GreaterThanOperator)],
                                        [typeof(GreaterThanOrEqualOperator)],
                                        [typeof(EqualityOperator)],
                                        [typeof(NotEqualityOperator)]],
            [typeof(BoolLiteral)] = [[typeof(TrueKeyword)],
                                     [typeof(FalseKeyword)]]
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
            if (root is not null && root.Children.Count == 1)
            { 
                root = root.Children[0] as ParseNode;
            }

            return messages;


            static ParseNode? parse(Type nonTerminal, List<IToken> tokens)
            {
                if (ParseNodes.TryGetValue(nonTerminal, out List<List<Type>>? value))
                {
                    foreach (var production in value)
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
                                if (tempList.Count > 0 && tempList[0].GetType() == production[i])//and the next token is what we want
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
                if (tokens[i] is WhiteSpace or Comment)
                {
                    tokens.RemoveAt(i);
                    i--;
                }
            }
        }
    }


    //[DebuggerDisplay("{this.GetType().Name}")]
    [AttributeUsage(AttributeTargets.Class)]
    public class OpensScopeAttribute : Attribute;

    public record class ParseNode
    {
        public virtual List<object> Children { get; protected set; } = [];
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

        public virtual ParseNode? Hoist()
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
            if (Token is null) return "NULL";

            return Token.Text;
        }
    }

    public record class Program() : ParseNode
    { 
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children[1] is Program prog)
            {
                Children.RemoveAt(1);
                Children.AddRange(prog.Children);
            }
            return this;
        }
    }
    public record class PossibleStatements : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children.Count > 1)
            {
                Children.RemoveAt(1);//remove ;
            }
            return (Children[0] as ParseNode)!;
        }
    }

    #region Variables
    public record class PrintStatement : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            Children.RemoveAt(0);//remove the print keyword
            Children.RemoveAt(0);//remove the (
            Children.RemoveAt(1);//remove the )


            //the stuff between the parens
            if (Children[0] is StringValue)
            {
                TypeExpected = "string";
                Children[0] = new ASTNode((Children[0] as IToken)!);
            }

            return this;
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
        public VariableName? Name { get; private set; }
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = (Children[0] as ParseNode)!.Location;

            if (Children[0] is VariableName vari)
            {
                Name = vari;
            }
            else
            {
                Name = new VariableName((Children[0] as ASTNode)!.Token.Text, "");
            }
            Children.RemoveAt(0);//remove the name token

            Children.RemoveAt(0);//remove the assignment operator

            return this;
        }
    }
    public record class VariableDeclaration : ParseNode
    {
        public string Name { get; private set; } = "";
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            TypeExpected = (Children[0] as IToken)!.Text;
            Children.RemoveAt(0);//remove the type token

            Name = (Children[0] as IToken)!.Text;
            Children.RemoveAt(0);//remove the name token

            if (Children.Count > 1)//if there is an assignment(more than just ;)
            {
                Children.RemoveAt(0);//remove the assignment operator
            }

            return this;
        }
    }
    public record class VariableDeclarationAndAssignment : VariableDeclaration;

    public record class VariableValue : ParseNode;

    public record class VariableName : ParseNode
    {

        public string Name { get; private set; } = "";
        public string Owner { get; private set; } = "";//for class variables, the name of the class that owns it

        public VariableName()
        {
        }
        public VariableName(string name, string owner)
        {
            Name = name;
            Owner = owner;
        }

        public override ParseNode Hoist()
        {
            base.Hoist();
            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);
            if (Children.Count > 1)
            { 
                Owner = (Children[0] as IToken)!.Text;
                Children.RemoveAt(0);
                Children.RemoveAt(0);//remove the dot
            }
            Name = (Children[0] as ASTNode)!.Token.Text;
            Children.Clear();
            return this;
        }
    }
    #endregion

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

    public record class Incrementer : ParseNode
    {
        public bool IsPre = false;
        public VariableName? Name { get; private set; }
        public bool IsIncrement = true;
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children[0] is IncrementOperator or DecrementOperator)
            {
                IsPre = true;
                if (Children[1] is VariableName name)
                {
                    Name = Children[1] as VariableName;
                }
                else
                {
                    var node = Children[1] as ASTNode;
                    Name = new VariableName(node!.Token.Text, "");
                }
                Children.RemoveAt(1);
            }
            else
            {
                if (Children[0] is VariableName name)
                {
                    Name = Children[0] as VariableName;
                }
                else
                {
                    var node = Children[0] as ASTNode;
                    Name = new VariableName(node!.Token.Text, "");
                }
                Children.RemoveAt(0);
            }

            IsIncrement = Children[0] is IncrementOperator;

            Children.Clear();//remove the operator
            TypeExpected = "int";

            return this;
        }
    }
    public record class ExpressionIncrementer : Incrementer;
    #endregion

    #region Flow control

    [OpensScope]
    public record class IfStatement : ParseNode
    {
        public ParseNode? Condition { get; private set; }
        public ParseNode? Body { get; private set; }
        public ParseNode? Followup { get; private set; }

        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Condition = Children[2] as ParseNode;

            if (Children[5] is not CloseCurlyBracket)//if there is a body
            {
                Body = Children[5] as ParseNode;
            }
            else
            {
                Body = null;
            }

            Followup = Children[^1] as ParseNode;

            Condition!.TypeExpected = "bool";//the condition should be a bool

#pragma warning disable CS8604 // Possible null reference argument.
            Children = new List<object> { Condition, Body, Followup }.Where(x => x != null).ToList();
#pragma warning restore CS8604 // Possible null reference argument.

            return this;
        }
    }

    [OpensScope]
    public record class IfntStatement : ParseNode
    {
        public ParseNode? Condition { get; private set; }
        public ParseNode? Body { get; private set; }
        public ParseNode? Followup { get; private set; }


        public override ParseNode Hoist()
        {
            base.Hoist();

            Condition = Children[2] as ParseNode;

            if (Children[5] is not CloseCurlyBracket)//if there is a body
            {
                Body = Children[5] as ParseNode;
            }
            else
            {
                Body = null;
            }

            Followup = Children[^1] as ParseNode;

            Condition!.TypeExpected = "bool";//the condition should be a bool

#pragma warning disable CS8604 // Possible null reference argument.
            Children = new List<object> { Condition, Body, Followup }.Where(x => x != null).ToList();
#pragma warning restore CS8604 // Possible null reference argument.

            return this;
        }
    }

    [OpensScope]
    public record class IfFollowUp : ParseNode
    {
        public override ParseNode? Hoist()
        {
            base.Hoist();

            switch (Children.Count)
            {
                case 4://else { }
                    var result = new ASTNode((Children[0] as IToken)!);
                    result.Children.Add(Children[2]);//the body
                    return result;

                case 2://else if or else ifn't
                    ParseNode result2 = (Children[1] as ParseNode)!;
                    //result2.Children.AddRange(Children[1..]);
                    return result2;

                case 0://epsilon
                    return null;
            }

            return this;
        }
    }

    [OpensScope]
    public record class WhileLoop : ParseNode
    {
        public ParseNode? Condition { get; private set; }
        public ParseNode? Body { get; private set; }
        public ParseNode? Followup { get; private set; }

        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Condition = Children[2] as ParseNode;

            if (Children[5] is not CloseCurlyBracket)//if there is a body
            {
                Body = Children[5] as ParseNode;
            }
            else
            {
                Body = null;
            }

            Followup = Children[^1] as ParseNode;

            Condition!.TypeExpected = "bool";//the condition should be a bool

#pragma warning disable CS8604 // Possible null reference argument.
            Children = new List<object> { Condition, Body, Followup }.Where(x => x != null).ToList();
#pragma warning restore CS8604 // Possible null reference argument.

            return this;
        }
    }

    [OpensScope]
    public record class WhileFollowUp : ParseNode
    {
        public override ParseNode? Hoist()
        {
            base.Hoist();

            var result = new ASTNode((Children[0] as IToken)!);
            result.Children.Add(Children[2]);//the body
            return result;
        }
    }

    [OpensScope]
    public record class ForLoop : ParseNode
    {
        public ParseNode? Initialization { get; private set; }
        public ParseNode? Condition { get; private set; }
        public ParseNode? Followup { get; private set; }
        public ParseNode? Body { get; private set; }

        public override ParseNode Hoist()
        {
            base.Hoist();
            //ForKeyword, OpenParenthesis, VariableDeclarationAndAssignment, Semicolon, BoolExpr, Semicolon, VariableAssignment, CloseParenthesis, OpenCurlyBracket, Program, CloseCurlyBracket
            //   0               1                        2                      3          4         5              6                  7                8              9           10
            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Initialization = Children[2] as ParseNode;
            Condition = Children[4] as ParseNode;
            Followup = Children[6] as ParseNode;

            if (Children[9] is not CloseCurlyBracket)//if there is a body
            {
                Body = Children[9] as ParseNode;
            }

            Condition!.TypeExpected = "bool";//the condition should be a bool

#pragma warning disable CS8604 // Possible null reference argument.
            Children = new List<object> { Initialization, Condition, Followup, Body }.Where(x => x != null).ToList();
#pragma warning restore CS8604 // Possible null reference argument.

            return this;
        }
    }

    public record class GotoStatement : ParseNode
    {
        public string LabelName { get; private set; } = "";
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            LabelName = (Children[1] as IToken)!.Text;

            Children.Clear();

            return this;
        }
    }
    #endregion

    #region Functions
    [OpensScope]
    public record class FunctionDeclaration : ParseNode
    {
        public string ReturnType { get; protected set; } = "";
        public string Name { get; protected set; } = "";

        public List<FunctionParameter> Parameters { get; protected set; } = [];


        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            ReturnType = (Children[0] as IToken)!.Text;
            Name = (Children[1] as IToken)!.Text;

            int curr = 0;
            Children.RemoveAt(curr); //remove the type
            Children.RemoveAt(curr); //remove the name
            Children.RemoveAt(curr); //remove the open parenthesis

            while ((Children[curr] as IToken) is not CloseParenthesis)//there are params
            {
                FunctionParameter param = (Children[curr] as FunctionParameter)!;
                Parameters.Add(param);

                if (param.Children.Count > 0)
                {
                    Children[curr] = param.Children[0];//replace with next param
                }
                else
                {
                    Children.RemoveAt(curr); //remove the close parenthesis
                }
            }
            Children.RemoveAt(curr); //remove the close parenthesis
            Children.RemoveAt(curr); //remove the open curly bracket

            if ((Children[curr] as IToken) is not CloseParenthesis)//if there is a body
            {
                curr++;
            }
            Children.RemoveAt(curr); //remove the close curly bracket

            return this;
        }
    }

    public record class FunctionCall : ParseNode
    {
        public string Name { get; private set; } = "";
        public string Owner { get; private set; } = "";
        public List<ASTNode> Parameters { get; private set; } = [];

        public Function? Target = null;

        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            if (Children[1] is Dot)
            { 
                Owner = (Children[0] as IToken)!.Text;
                Children.RemoveAt(0);//remove the owner
                Children.RemoveAt(0);//remove the dot
            }

            Name = (Children[0] as IToken)!.Text;

            Children.RemoveAt(0); //remove the name
            Children.RemoveAt(0); //remove the open parenthesis

            while ((Children[0] as IToken) is not CloseParenthesis)//there are params
            {
                if (Children[0] is ASTNode node)
                {
                    Parameters.Add(node);
                    Children.RemoveAt(0);
                }
                else
                {
                    FunctionCallParameter param = (Children[0] as FunctionCallParameter)!;

                    Parameters.Add((param.Children[0] as ASTNode)!);

                    Children[0] = param.Children[1];//replace with next param
                }
            }

            Children.Clear();

            return this;
        }
    }

    public record class FunctionParameter : ParseNode
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

            if (Children.Count > 1)//if there are more after
            {
                Children.RemoveAt(0);//remove the comma
            }

            return this;
        }
    }

    public record class FunctionCallParameter : ParseNode
    {
        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as ASTNode)!.Token.Row, (Children[0] as ASTNode)!.Token.Column);

            if (Children.Count > 1)//if there are more after
            {
                Children.RemoveAt(1);//remove the comma
            }

            return this;
        }
    }

    public record class ReturnStatement : ParseNode
    {
        public ParseNode? Value { get; private set; } = null;
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children.Count > 1)
            { 
                Value = Children[1] as ParseNode;
            }

            Children.Clear();

            return this;
        }
    }
    #endregion

    #region Classes
    [OpensScope]
    public record class ClassDeclaration : ParseNode
    {
        public string Name { get; private set; } = "";

        public override ParseNode Hoist()
        {
            base.Hoist();

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            Name = (Children[1] as IToken)!.Text;

            Children.RemoveAt(0); //remove the class keyword
            Children.RemoveAt(0); //remove the name
            Children.RemoveAt(0); //remove the open curly bracket
            Children.RemoveAt(Children.Count - 1); //remove the close curly bracket
            if (Children[0] is ClassBody body)
            {
                Children.RemoveAt(0);
                Children.AddRange(body.Children);
            }

            return this;
        }
    }

    public record class ClassBody : ParseNode
    { 
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children[1] is ClassBody body)
            {
                Children.RemoveAt(1);
                Children.AddRange(body.Children);
            }
            return this;
        }
    }
    public record class ClassMember : ParseNode
    { 
        public override ParseNode Hoist()
        {
            base.Hoist();

            if (Children[^1] is Semicolon)
            {
                Children.RemoveAt(1);
            }
            return (Children[0] as ParseNode)!;
        }
    }

    [OpensScope]
    public record class ConstructorDeclaration : FunctionDeclaration
    {
        public override ParseNode Hoist()
        {
            //base.Hoist(), but I don't want to call the FunctionDeclaration hoist
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

            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);

            ReturnType = (Children[0] as IToken)!.Text;
            Name = (Children[0] as IToken)!.Text;

            int curr = 0;
            Children.RemoveAt(curr); //remove the type
            Children.RemoveAt(curr); //remove the open parenthesis

            while ((Children[curr] as IToken) is not CloseParenthesis)//there are params
            {
                FunctionParameter param = (Children[curr] as FunctionParameter)!;
                Parameters.Add(param);

                if (param.Children.Count > 0)
                {
                    Children[curr] = param.Children[0];//replace with next param
                }
                else
                {
                    Children.RemoveAt(curr); //remove the close parenthesis
                }
            }
            Children.RemoveAt(curr); //remove the close parenthesis
            Children.RemoveAt(curr); //remove the open curly bracket

            if ((Children[curr] as IToken) is not CloseParenthesis)//if there is a body
            {
                curr++;
            }
            Children.RemoveAt(curr); //remove the close curly bracket

            return this;
        }
    }

    public record class ClassInstantiation : ParseNode
    {
        public string ClassName { get; private set; } = "";
        public List<ASTNode> Parameters { get; private set; } = [];
        public override ParseNode Hoist()
        {
            base.Hoist();
            Location = ((Children[0] as IToken)!.Row, (Children[0] as IToken)!.Column);
            Children.RemoveAt(0);//remove the new keyword
            ClassName = (Children[0] as IToken)!.Text;
            Children.RemoveAt(0); //remove the class name
            Children.RemoveAt(0); //remove the open parenthesis
            TypeExpected = ClassName;//the type of a class instantiation is the class
            while ((Children[0] as IToken) is not CloseParenthesis)//there are params
            {
                if (Children[0] is ASTNode node)
                {
                    Parameters.Add(node);
                    Children.RemoveAt(0);
                }
                else
                {
                    FunctionCallParameter param = (Children[0] as FunctionCallParameter)!;
                    Parameters.Add((param.Children[0] as ASTNode)!);
                    Children[0] = param.Children[1];//replace with next param
                }
            }
            Children.Clear();
            return this;
        }
    }
    #endregion

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