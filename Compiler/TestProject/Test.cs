using Compiler;
using Compiler.Tokens;

using Mono.Cecil;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;


//Excellent — now we’re truly debugging the cake. 🍰

namespace TestProject
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Tokenizer
    {
        public static void ValidLexTest(string program, params Type[] expectedTokenTypes)
        {
            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result), "Lexer reported failure.");

            var nonWhitespace = result.Where(t => t is not WhiteSpace and not Comment).ToList();

            Assert.AreEqual(expectedTokenTypes.Length, nonWhitespace.Count,
                $"Expected {expectedTokenTypes.Length} tokens but got {nonWhitespace.Count}:\n" +
                string.Join(", ", nonWhitespace.Select(t => t.GetType().Name)));

            for (int i = 0; i < expectedTokenTypes.Length; i++)
            {
                Assert.IsInstanceOfType(nonWhitespace[i], expectedTokenTypes[i],
                    $"Token {i}: expected {expectedTokenTypes[i].Name} but got {nonWhitespace[i].GetType().Name} (\"{nonWhitespace[i].Text}\")");
            }
        }
        public static void InvalidLexTest(string program)
        {
            bool success = Lexer.Lex(program, out List<IToken> result);

            Assert.IsFalse(success, "Expected lex failure but lexed successfully.");
            Assert.IsTrue(result.Any(t => t is Error), "Expected an Error token.");
        }

        #region Keywords
        [TestMethod, TestCategory("Keyword")]
        public void PrintKeyword()
        {
            ValidLexTest("print", typeof(PrintKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void IfKeyword()
        {
            ValidLexTest("if", typeof(IfKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void IfntKeyword()
        {
            ValidLexTest("ifn't", typeof(IfntKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void ElseKeyword()
        {
            ValidLexTest("else", typeof(ElseKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void WhileKeyword()
        {
            ValidLexTest("while", typeof(WhileKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void ForKeyword()
        {
            ValidLexTest("for", typeof(ForKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void TrueKeyword()
        {
            ValidLexTest("true", typeof(TrueKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void FalseKeyword()
        {
            ValidLexTest("false", typeof(FalseKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void GotoKeyword()
        {
            ValidLexTest("goto", typeof(GotoKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void ReturnKeyword()
        {
            ValidLexTest("return", typeof(ReturnKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void ClassKeyword()
        {
            ValidLexTest("class", typeof(ClassKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void NewKeyword()
        {
            ValidLexTest("new", typeof(NewKeyword));
        }

        [TestMethod, TestCategory("Keyword")]
        public void KeywordAsIdentifierPrefix()
        {
            // "format" starts with "for" but should be an Identifier, not ForKeyword
            ValidLexTest("format", typeof(Identifier));
        }
        #endregion

        #region Punctuation
        [TestMethod, TestCategory("Punctuation")]
        public void OpenParenthesis()
        {
            ValidLexTest("(", typeof(OpenParenthesis));
        }

        [TestMethod, TestCategory("Punctuation")]
        public void CloseParenthesis()
        {
            ValidLexTest(")", typeof(CloseParenthesis));
        }

        [TestMethod, TestCategory("Punctuation")]
        public void SemicolonToken()
        {
            ValidLexTest(";", typeof(Semicolon));
        }

        [TestMethod, TestCategory("Punctuation")]
        public void CommaToken()
        {
            ValidLexTest(",", typeof(Comma));
        }

        [TestMethod, TestCategory("Punctuation")]
        public void DotToken()
        {
            ValidLexTest(".", typeof(Dot));
        }

        [TestMethod, TestCategory("Punctuation")]
        public void OpenCurlyBracket()
        {
            ValidLexTest("{", typeof(OpenCurlyBracket));
        }

        [TestMethod, TestCategory("Punctuation")]
        public void CloseCurlyBracket()
        {
            ValidLexTest("}", typeof(CloseCurlyBracket));
        }
        #endregion

        #region Operators
        [TestMethod, TestCategory("Operator")]
        public void AssignmentOperator()
        {
            ValidLexTest("=", typeof(AssignmentOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void EqualityOperator()
        {
            ValidLexTest("=?", typeof(EqualityOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void NotEqualityOperator()
        {
            ValidLexTest("!=", typeof(NotEqualityOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void PlusOperator()
        {
            ValidLexTest("+", typeof(PlusOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void MinusOperator()
        {
            ValidLexTest("-", typeof(MinusOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void TimesOperator()
        {
            ValidLexTest("*", typeof(TimesOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void DivideOperator()
        {
            ValidLexTest("/", typeof(DivideOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void IncrementOperator()
        {
            ValidLexTest("++", typeof(IncrementOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void DecrementOperator()
        {
            ValidLexTest("--", typeof(DecrementOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void AndOperator()
        {
            ValidLexTest("&&", typeof(AndOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void OrOperator()
        {
            ValidLexTest("||", typeof(OrOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void NotOperator()
        {
            ValidLexTest("!", typeof(NotOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void GreaterThanOperator()
        {
            ValidLexTest(">", typeof(GreaterThanOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void LessThanOperator()
        {
            ValidLexTest("<", typeof(LessThanOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void GreaterThanOrEqualOperator()
        {
            ValidLexTest(">=", typeof(GreaterThanOrEqualOperator));
        }

        [TestMethod, TestCategory("Operator")]
        public void LessThanOrEqualOperator()
        {
            ValidLexTest("<=", typeof(LessThanOrEqualOperator));
        }
        #endregion

        #region Values
        [TestMethod, TestCategory("Value")]
        public void StringLiteral()
        {
            ValidLexTest("\"Hello\"", typeof(StringValue));
        }

        [TestMethod, TestCategory("Value")]
        public void StringWithEscapedQuote()
        {
            ValidLexTest("\"He said \\\"hi\\\"\"", typeof(StringValue));
        }

        [TestMethod, TestCategory("Value")]
        public void IntegerLiteral()
        {
            ValidLexTest("42", typeof(NumericValue));
        }

        [TestMethod, TestCategory("Value")]
        public void DecimalLiteral()
        {
            ValidLexTest("3.14", typeof(NumericValue));
        }

        [TestMethod, TestCategory("Value")]
        public void ScientificNotation()
        {
            ValidLexTest("1e10", typeof(NumericValue));
        }
        #endregion

        #region Identifiers and labels
        [TestMethod, TestCategory("Identifier")]
        public void SimpleIdentifier()
        {
            ValidLexTest("myVar", typeof(Identifier));
        }

        [TestMethod, TestCategory("Identifier")]
        public void IdentifierWithUnderscore()
        {
            ValidLexTest("_count", typeof(Identifier));
        }

        [TestMethod, TestCategory("Identifier")]
        public void IdentifierWithDigits()
        {
            ValidLexTest("x2", typeof(Identifier));
        }

        [TestMethod, TestCategory("Label")]
        public void LabelToken()
        {
            ValidLexTest("LoopStart:", typeof(Label));
        }
        #endregion

        #region Whitespace and comments
        [TestMethod, TestCategory("Whitespace")]
        public void WhitespaceIsSkipped()
        {
            ValidLexTest("   int   a   ", typeof(Identifier), typeof(Identifier));
        }

        [TestMethod, TestCategory("Comment")]
        public void SingleLineComment()
        {
            ValidLexTest("int a // this is a comment", typeof(Identifier), typeof(Identifier));
        }
        #endregion

        #region Composite statements
        [TestMethod, TestCategory("Composite")]
        public void PrintStatement()
        {
            ValidLexTest("print(\"Hello\");",
                typeof(PrintKeyword), typeof(OpenParenthesis), typeof(StringValue), typeof(CloseParenthesis), typeof(Semicolon));
        }

        [TestMethod, TestCategory("Composite")]
        public void VariableDeclarationAndAssignment()
        {
            ValidLexTest("int a = 5;",
                typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator), typeof(NumericValue), typeof(Semicolon));
        }

        [TestMethod, TestCategory("Composite")]
        public void MathExpression()
        {
            ValidLexTest("3 + 4 * 5",
                typeof(NumericValue), typeof(PlusOperator), typeof(NumericValue), typeof(TimesOperator), typeof(NumericValue));
        }

        [TestMethod, TestCategory("Composite")]
        public void BoolExpression()
        {
            ValidLexTest("a > 3 && b <= 5 || !c",
                typeof(Identifier), typeof(GreaterThanOperator), typeof(NumericValue),
                typeof(AndOperator),
                typeof(Identifier), typeof(LessThanOrEqualOperator), typeof(NumericValue),
                typeof(OrOperator),
                typeof(NotOperator), typeof(Identifier));
        }

        [TestMethod, TestCategory("Composite")]
        public void ForLoopHeader()
        {
            ValidLexTest("for(int i = 0; i < 14; i++)",
                typeof(ForKeyword), typeof(OpenParenthesis),
                typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator), typeof(NumericValue), typeof(Semicolon),
                typeof(Identifier), typeof(LessThanOperator), typeof(NumericValue), typeof(Semicolon),
                typeof(Identifier), typeof(IncrementOperator),
                typeof(CloseParenthesis));
        }

        [TestMethod, TestCategory("Composite")]
        public void ClassInstantiation()
        {
            ValidLexTest("Cat bob = new Cat();",
                typeof(Identifier), typeof(Identifier), typeof(AssignmentOperator),
                typeof(NewKeyword), typeof(Identifier), typeof(OpenParenthesis), typeof(CloseParenthesis), typeof(Semicolon));
        }

        [TestMethod, TestCategory("Composite")]
        public void DotAccess()
        {
            ValidLexTest("bob.Name",
                typeof(Identifier), typeof(Dot), typeof(Identifier));
        }

        [TestMethod, TestCategory("Composite")]
        public void GotoStatement()
        {
            ValidLexTest("goto EndLoop;",
                typeof(GotoKeyword), typeof(Identifier), typeof(Semicolon));
        }

        [TestMethod, TestCategory("Composite")]
        public void IfntStatement()
        {
            ValidLexTest("ifn't(a <= 22)",
                typeof(IfntKeyword), typeof(OpenParenthesis),
                typeof(Identifier), typeof(LessThanOrEqualOperator), typeof(NumericValue),
                typeof(CloseParenthesis));
        }

        [TestMethod, TestCategory("Composite")]
        public void EqualityCheck()
        {
            ValidLexTest("a =? b",
                typeof(Identifier), typeof(EqualityOperator), typeof(Identifier));
        }
        #endregion

        #region Error tokens
        [TestMethod, TestCategory("Error")]
        public void UnrecognizedCharacter()
        {
            InvalidLexTest("@");
        }

        [TestMethod, TestCategory("Error")]
        public void UnrecognizedInMiddle()
        {
            InvalidLexTest("int a = 5 @ 3;");
        }
        #endregion

        #region Row and column tracking
        [TestMethod, TestCategory("Position")]
        public void RowTracking()
        {
            Lexer.Lex("int a\nint b", out var result);
            var identifiers = result.Where(t => t is Identifier).ToList();
            Assert.AreEqual(0, identifiers[0].Row);
            Assert.AreEqual(1, identifiers[2].Row);
        }

        [TestMethod, TestCategory("Position")]
        public void ColumnTracking()
        {
            Lexer.Lex("int a = 5;", out var result);
            var nonWs = result.Where(t => t is not WhiteSpace).ToList();
            // "int" at column 0, "a" at column 4, "=" at column 6, "5" at column 8, ";" at column 9
            Assert.AreEqual(0, nonWs[0].Column);
            Assert.AreEqual(4, nonWs[1].Column);
        }
        #endregion
    }

    [TestClass]
    [DoNotParallelize]
    public sealed class Parsing
    {
        public static void ValidParseTest(string program)
        {
            List<string> messages;

            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result), "Did not tokenize.");

            messages = Parser.Parse(result, out ParseNode? tree);

            Assert.IsTrue(messages.Count == 0, string.Join("\n", messages));
        }
        public static void InvalidParseTest(string program)
        {
            List<string> messages;

            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result), "Did not tokenize.");

            messages = Parser.Parse(result, out ParseNode? tree);

            Assert.IsTrue(messages.Count > 0, "Expected parse failure but parsed successfully.");
        }

        #region Print statements
        [TestMethod, TestCategory("Print")]
        public void PrintStringLiteral()
        {
            string program = "print(\"Hello\");\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Print")]
        public void PrintVariable()
        {
            string program =
                "int a = 5;\n" +
                "print(a);\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Print")]
        public void PrintMissingParenthesis()
        {
            string program = "print(\"Hello\";\n";
            InvalidParseTest(program);
        }

        [TestMethod, TestCategory("Print")]
        public void PrintMissingSemicolon()
        {
            string program = "print(\"Hello\")\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Variable declarations
        [TestMethod, TestCategory("Variables")]
        public void VariableDeclaration()
        {
            string program = "int a;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void VariableDeclarationAndAssignment()
        {
            string program = "int a = 5;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void VariableAssignment()
        {
            string program =
                "int a = 1;\n" +
                "a = 2;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void StringVariableDeclaration()
        {
            string program = "string b = \"Hello\";\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void BoolVariableDeclaration()
        {
            string program = "bool c = true;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void MultipleDeclarations()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "bool c = true;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void MissingAssignmentOperator()
        {
            string program = "int a 5;\n";
            InvalidParseTest(program);
        }

        [TestMethod, TestCategory("Variables")]
        public void MissingSemicolonOnDeclaration()
        {
            string program = "int a = 5\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Math expressions
        [TestMethod, TestCategory("Math")]
        public void SimpleMathAddition()
        {
            string program = "int a = 3 + 4;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Math")]
        public void MathWithPrecedence()
        {
            string program = "int a = 3 + 4 * 5;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Math")]
        public void MathWithParentheses()
        {
            string program = "int a = (1 + 2) * 3;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Math")]
        public void NestedMathParentheses()
        {
            string program = "int a = 3 + 2 * (4 - 1) / 5;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Math")]
        public void MathLeftAssociativity()
        {
            string program = "int a = 5 - 3 - 2;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Math")]
        public void MathMultipleStatements()
        {
            string program =
                "int a = 3 + 4;\n" +
                "int b = 3 + 4 * 5;\n" +
                "int c = (1 + 2) * 3;\n" +
                "int d = 4 * (7 - 2);\n" +
                "int e = 42;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Math")]
        public void MathUnmatchedParenthesis()
        {
            string program = "int a = (3 + 4;\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Incrementers
        [TestMethod, TestCategory("Incrementer")]
        public void PostfixIncrement()
        {
            string program =
                "int a = 3;\n" +
                "a++;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Incrementer")]
        public void PrefixIncrement()
        {
            string program =
                "int a = 3;\n" +
                "++a;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Incrementer")]
        public void PostfixDecrement()
        {
            string program =
                "int a = 3;\n" +
                "a--;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Incrementer")]
        public void PrefixDecrement()
        {
            string program =
                "int a = 3;\n" +
                "--a;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Incrementer")]
        public void IncrementerInExpression()
        {
            string program =
                "int a = 3;\n" +
                "int b = a++ + 2;\n";
            ValidParseTest(program);
        }
        #endregion

        #region If statements
        [TestMethod, TestCategory("If")]
        public void SimpleIf()
        {
            string program =
                "if(a > 3)\n" +
                "{\n" +
                "    int e = 42;\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("If")]
        public void IfElse()
        {
            string program =
                "if(a > 3)\n" +
                "{\n" +
                "    print(\"Hi\");\n" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Bye\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("If")]
        public void IfElseIfElse()
        {
            string program =
                "int a = 200;\n" +
                "if(a <= 37)\n" +
                "{\n" +
                "    print(\"Hi\");\n" +
                "}\n" +
                "else if(a < 42)\n" +
                "{\n" +
                "    print(\"Hey\");\n" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Sup\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("If")]
        public void IfComplexBoolExpr()
        {
            string program =
                "if(a > 3 && !(b || c) || d)\n" +
                "{\n" +
                "    int e = 42;\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("If")]
        public void IfMissingBraces()
        {
            string program =
                "if(a > 3)\n" +
                "    int e = 42;\n";
            InvalidParseTest(program);
        }

        [TestMethod, TestCategory("If")]
        public void IfMissingCondition()
        {
            string program =
                "if()\n" +
                "{\n" +
                "    int e = 42;\n" +
                "}\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Ifn't statements
        [TestMethod, TestCategory("Ifn't")]
        public void SimpleIfnt()
        {
            string program =
                "ifn't(a <= 22)\n" +
                "{\n" +
                "    print(\"Hello\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Ifn't")]
        public void IfntElseIfnt()
        {
            string program =
                "ifn't(a <= 22)\n" +
                "{\n" +
                "    print(\"Hello\");\n" +
                "}\n" +
                "else ifn't(a != 2)\n" +
                "{\n" +
                "    print(\"Yay\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Ifn't")]
        public void MixedIfAndIfnt()
        {
            string program =
                "int a = 200;\n" +
                "if(a <= 37)\n" +
                "{\n" +
                "    print(\"Hi\");\n" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Sup\");\n" +
                "}\n" +
                "ifn't(a <= 22)\n" +
                "{\n" +
                "    print(\"Hello\");\n" +
                "}\n" +
                "else ifn't(a != 2)\n" +
                "{\n" +
                "    print(\"Yay\");\n" +
                "}\n";
            ValidParseTest(program);
        }
        #endregion

        #region While loops
        [TestMethod, TestCategory("While")]
        public void SimpleWhile()
        {
            string program =
                "while(x < 3)\n" +
                "{\n" +
                "    x = x + 1;\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("While")]
        public void WhileElse()
        {
            string program =
                "while(x < 3)\n" +
                "{\n" +
                "    x = x + 1;\n" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Done\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("While")]
        public void WhileMissingBraces()
        {
            string program =
                "while(x < 3)\n" +
                "    x = x + 1;\n";
            InvalidParseTest(program);
        }
        #endregion

        #region For loops
        [TestMethod, TestCategory("For")]
        public void ForWithDeclaration()
        {
            string program =
                "for(int i = 0; i < 14; i++)\n" +
                "{\n" +
                "    print(i);\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("For")]
        public void ForWithAssignment()
        {
            string program =
                "int a = 2;\n" +
                "for(a = 0; a < 14; a = a + 1)\n" +
                "{\n" +
                "    print(a);\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("For")]
        public void ForMissingSemicolonInHeader()
        {
            string program =
                "for(int i = 0 i < 14; i++)\n" +
                "{\n" +
                "    print(i);\n" +
                "}\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Goto and labels
        [TestMethod, TestCategory("Goto")]
        public void GotoAndLabel()
        {
            string program =
                "int i = 0;\n" +
                "LoopLabel:\n" +
                "print(\"Loopy\");\n" +
                "if(i > 5)\n" +
                "{\n" +
                "    goto EndLoop;\n" +
                "}\n" +
                "i = i + 1;\n" +
                "goto LoopLabel;\n" +
                "EndLoop:\n" +
                "print(\"Done\");\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Goto")]
        public void GotoMissingSemicolon()
        {
            string program =
                "LoopLabel:\n" +
                "goto LoopLabel\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Functions
        [TestMethod, TestCategory("Function")]
        public void FunctionDeclarationNoParams()
        {
            string program =
                "void Foo()\n" +
                "{\n" +
                "    print(\"Bar\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Function")]
        public void FunctionDeclarationWithParams()
        {
            string program =
                "void Foo(int a, string b)\n" +
                "{\n" +
                "    print(b);\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Function")]
        public void FunctionCallNoArgs()
        {
            string program =
                "void Foo()\n" +
                "{\n" +
                "    print(\"Bar\");\n" +
                "}\n" +
                "Foo();\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Function")]
        public void FunctionCallWithArgs()
        {
            string program =
                "void Foo(int a, string b)\n" +
                "{\n" +
                "    print(b);\n" +
                "}\n" +
                "Foo(4, \"Hello\");\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Function")]
        public void FunctionWithReturnValue()
        {
            string program =
                "int Pow(int a, int b)\n" +
                "{\n" +
                "    int result = 1;\n" +
                "    for(int i = 0; i < b; i++)\n" +
                "    {\n" +
                "        result = result * a;\n" +
                "    }\n" +
                "    return result;\n" +
                "}\n" +
                "int a = Pow(2, 3);\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Function")]
        public void ReturnNoValue()
        {
            string program =
                "void Foo()\n" +
                "{\n" +
                "    return;\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Function")]
        public void FunctionCallMissingCloseParen()
        {
            string program =
                "void Foo()\n" +
                "{\n" +
                "    print(\"Bar\");\n" +
                "}\n" +
                "Foo(;\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Boolean expressions
        [TestMethod, TestCategory("Boolean")]
        public void SimpleComparison()
        {
            string program =
                "if(a > 3)\n" +
                "{\n" +
                "    print(\"Yes\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Boolean")]
        public void BoolLiteralCondition()
        {
            string program =
                "if(true)\n" +
                "{\n" +
                "    print(\"Yes\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Boolean")]
        public void BoolAndOr()
        {
            string program =
                "if(a > 3 && b < 5 || c > 1)\n" +
                "{\n" +
                "    print(\"Yes\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Boolean")]
        public void BoolNot()
        {
            string program =
                "if(!a)\n" +
                "{\n" +
                "    print(\"Yes\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Boolean")]
        public void BoolNestedParentheses()
        {
            string program =
                "if(a > 3 && !(b || c) || !d)\n" +
                "{\n" +
                "    print(\"Yes\");\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Boolean")]
        public void AllComparisonOperators()
        {
            string program =
                "if(a < 1) { print(\"a\"); }\n" +
                "if(a <= 1) { print(\"b\"); }\n" +
                "if(a > 1) { print(\"c\"); }\n" +
                "if(a >= 1) { print(\"d\"); }\n" +
                "if(a =? 1) { print(\"e\"); }\n" +
                "if(a != 1) { print(\"f\"); }\n";
            ValidParseTest(program);
        }
        #endregion

        #region Classes
        [TestMethod, TestCategory("Class")]
        public void ClassDeclaration()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n" +
                "    int Age;\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Class")]
        public void ClassWithMethods()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n" +
                "    int Age;\n" +
                "    void SetName(string name)\n" +
                "    {\n" +
                "        Name = name;\n" +
                "    }\n" +
                "    int SetAge(int age)\n" +
                "    {\n" +
                "        Age = age;\n" +
                "        return age;\n" +
                "    }\n" +
                "}\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Class")]
        public void ClassInstantiation()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n" +
                "}\n" +
                "Cat bob = new Cat();\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Class")]
        public void ClassConstructorWithParams()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n" +
                "    int Age;\n" +
                "    Cat(string name, int age)\n" +
                "    {\n" +
                "        Name = name;\n" +
                "        Age = age;\n" +
                "    }\n" +
                "}\n" +
                "Cat bob = new Cat(\"Bob\", 2);\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Class")]
        public void ClassFieldAccess()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n" +
                "}\n" +
                "Cat bob = new Cat();\n" +
                "bob.Name = \"Bob\";\n" +
                "print(bob.Name);\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Class")]
        public void ClassMethodCall()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n" +
                "    void SetName(string name)\n" +
                "    {\n" +
                "        Name = name;\n" +
                "    }\n" +
                "}\n" +
                "Cat bob = new Cat();\n" +
                "bob.SetName(\"Bob\");\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Class")]
        public void ClassMissingClosingBrace()
        {
            string program =
                "class Cat\n" +
                "{\n" +
                "    string Name;\n";
            InvalidParseTest(program);
        }
        #endregion

        #region Scope and combined
        [TestMethod, TestCategory("Scope")]
        public void NestedScopes()
        {
            string program =
                "int a = 2;\n" +
                "if(a > 3 && !(b || c) || !d)\n" +
                "{\n" +
                "    int e = 42;\n" +
                "    int b;\n" +
                "    a = 14;\n" +
                "}\n" +
                "e = 3;\n";
            ValidParseTest(program);
        }

        [TestMethod, TestCategory("Scope")]
        public void MultipleStatementTypes()
        {
            string program =
                "int x = 5;\n" +
                "print(\"Start\");\n" +
                "if(x > 3)\n" +
                "{\n" +
                "    print(\"Big\");\n" +
                "}\n" +
                "for(int i = 0; i < x; i++)\n" +
                "{\n" +
                "    print(i);\n" +
                "}\n" +
                "while(x > 0)\n" +
                "{\n" +
                "    x = x - 1;\n" +
                "}\n";
            ValidParseTest(program);
        }
        #endregion
    }

    [TestClass]
    [DoNotParallelize]
    public sealed class Semantic
    {
        public static void ValidSemanticsTest(string program)
        {
            List<string> messages;

            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result));

            messages = Parser.Parse(result, out ParseNode? twee);

            if (messages.Count > 0) throw new Exception("Did not parse.");

            messages = SemanticAnalyzer.Analyze(twee!, out var scopes);

            if (messages.Count > 0) throw new Exception("Did not pass analysis.");
        }
        public static void InvalidSemanticsTest(string program)
        {
            List<string> messages;

            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result), "Did not tokenize.");

            messages = Parser.Parse(result, out ParseNode? twee);

            Assert.IsFalse(messages.Count > 0, "Did not parse.");

            messages = SemanticAnalyzer.Analyze(twee!, out var scopes);

            Assert.IsTrue(messages.Count > 0, "Type mismatch missed.");
        }

        #region Successful type checks
        [TestMethod, TestCategory("Successful type check")]
        public void StandardTypeTest()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "bool c = true;\n" +

                "a = 1;\n" +
                "b = \"Bye\";\n" +
                "c = false;\n" +

                "print(a);\n" +
                "print(b);\n" +
                "print(c);\n";
            ValidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Successful type check")]
        public void FunctionParamTypeTest()
        {
            string program =
                "Foo(4, \"Hello\");\r\n" +
                "void Foo(int a, string b)\r\n" +
                "{\r\n" +
                    "\tfor(string i = 0; i < a; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(b);\r\n" +
                    "\t}\r\n" +
                "}\r\n";
            ValidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Successful type check")]
        public void FunctionReturnTypeTest()
        {
            string program =
                "int a = Pow(2, 3);\r\n" +
                "print(a);" +
                "int Pow(int a, int b)\r\n" +
                "{\r\n" +
                    "\tint result = 1;\r\n" +
                    "\tfor(int i = 0; i < b; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tresult = result * a;\r\n" +
                    "\t}\r\n" +
                    "\treturn result;\r\n" +
                "}\r\n" +
                "\r\n";
            ValidSemanticsTest(program);
        }

        [TestMethod, TestCategory("Successful type check"), TestCategory("Classes")]
        public void BasicClassTest()
        {
            string program =
                "Cat bob = new Cat();\r\n" +
                "bob.Name = \"Bob\";\r\n" +
                "print(bob.Name);\r\n" +
                "print(bob.Age);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                "    string Name;\r\n" +
                "    int Age;\r\n" +
                "    \r\n" +
                "    void SetName(string name)\r\n" +
                "    {\r\n" +
                "        Name = name;\r\n" +
                "    }\r\n" +
                "    int SetAge(int age)\r\n" +
                "    {\r\n" +
                "        Age = age;\r\n" +
                "        return age;\r\n" +
                "    }\r\n" +
                "}";
            ValidSemanticsTest(program);
        }
        #endregion


        #region Basic type mismatch tests
        /*
        "int a = 2;\n" +
        "string b = \"Hi\";\n" +
        "bool c = true;\n" +

        "a = 1;\n" +
        "a = \"Bye\";\n" +
        "a = false;\n" +
        "a = b;\n" +
        "a = c;\n" +

        "b = 2;\n" +
        "b = \"Bye\";\n" +
        "b = true;\n" +
        "b = a;\n" +
        "b = c;\n" +

        "c = 14;\n" +
        "c = \"Hello\";\n" +
        "c = true;\n" +
        "c = a;\n" +
        "c = b;\n" +

        "if(a > b && c || b){}\n" +

        "print(a);\n" +
        "print(b);\n" +
        "print(c);\n" +
        "print(\"Hi\");\n"
        */
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitIntWithBool()
        {
            string program = "int a = true;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitIntWithString()
        {
            string program = "int a = \"Hi\";\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToString()
        {
            string program =
                "int a = 2;\n" +
                "a = \"Bye\";\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToBool()
        {
            string program =
                "int a = 2;\n" +
                "a = false;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToStringVar()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "a = b;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToBoolVar()
        {
            string program =
                "int a = 2;\n" +
                "bool c = true;\n" +
                "a = c;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitStringWithInt()
        {
            string program = "string b = 3;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitStringWithBool()
        {
            string program = "string b = true;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToInt()
        {
            string program =
                "string b = \"Hi\";\n" +
                "b = 2;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToBool()
        {
            string program =
                "string b = \"Hi\";\n" +
                "b = false;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToIntVar()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "b = a;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToBoolVar()
        {
            string program =
                "string b = \"Hi\";\n" +
                "bool c = true;\n" +
                "b = c;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitBoolWithInt()
        {
            string program = "bool c = 3;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitBoolWithString()
        {
            string program = "bool c = \"Hi\";\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToInt()
        {
            string program =
                "bool c = true;\n" +
                "c = 2;\n";
            InvalidSemanticsTest(program);

        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToString()
        {
            string program =
                "bool c = true;\n" +
                "c = \"Hi\";\n";
            InvalidSemanticsTest(program);

        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToIntVar()
        {
            string program =
                "int a = 2;\n" +
                "bool c = true;\n" +
                "c = a;\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToStringVar()
        {
            string program =
                "string b = \"Hi\";\n" +
                "bool c = true;\n" +
                "c = b;\n";
            InvalidSemanticsTest(program);
        }
        #endregion

        #region Function type mismatch
        [TestMethod, TestCategory("Function type mismatch")]
        public void FunctionParamTypeMismatchTest()
        {
            string program =
                "Foo(4, true);\r\n" +
                "void Foo(int a, string b)\r\n" +
                "{\r\n" +
                    "\tfor(string i = 0; i < a; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(b);\r\n" +
                    "\t}\r\n" +
                "}\r\n";
            InvalidSemanticsTest(program);
        }
        [TestMethod, TestCategory("Function type mismatch")]
        public void FunctionReturnTypeMismatchTest()
        {
            string program =
                "string a = Pow(2, 3);\r\n" +
                "print(a);" +
                "int Pow(int a, int b)\r\n" +
                "{\r\n" +
                    "\tint result = 1;\r\n" +
                    "\tfor(int i = 0; i < b; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tresult = result * a;\r\n" +
                    "\t}\r\n" +
                    "\treturn result;\r\n" +
                "}\r\n" +
                "\r\n";
            InvalidSemanticsTest(program);
        }

        [TestMethod, TestCategory("Function type mismatch"), TestCategory("Classes")]
        public void FunctionReturnTypeInsideClassMismatchTest()
        {
            string program =
                "Cat bob = new Cat();\r\n" +
                "bob.Name = \"Bob\";\r\n" +
                "string a = bob.SetAge(2);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                "    string Name;\r\n" +
                "    int Age;\r\n" +
                "    \r\n" +
                "    void SetName(string name)\r\n" +
                "    {\r\n" +
                "        Name = name;\r\n" +
                "    }\r\n" +
                "    int SetAge(int age)\r\n" +
                "    {\r\n" +
                "        Age = age;\r\n" +
                "        return age;\r\n" +
                "    }\r\n" +
                "}";
            InvalidSemanticsTest(program);
        }
        #endregion

        #region Scope checking
        [TestMethod, TestCategory("Scope checking")]
        public void ScopeTestTest()
        {
            string program =
                "int a = 2;\n" +
                "if(a > 3)\n" +
                "{\n" +
                "   int e = 42;\n" +
                "   int b;\n" +
                "   a = 14;\n" +
                "}\n" +
                "e = 3;";
            InvalidSemanticsTest(program);
        }
        #endregion
    }

    [TestClass]
    [DoNotParallelize]
    public sealed class FullProgram
    {
        #region Helpers
        private static string LogTree(ParseNode node, int depth = 0)
        {
            StringBuilder sb = new();
            sb.AppendLine(new string(' ', depth * 2) + (node is ASTNode ? node : node.GetType().Name));
            foreach (var child in node.Children)
            {
                if (child is ParseNode childNode)
                {
                    sb.AppendLine(LogTree(childNode, depth + 1));
                }
            }

            return sb.ToString();
        }

        private static void CheckEntryPoint(string file)
        {
            using var pe = new PEReader(File.OpenRead(file));

            var cor = pe.PEHeaders.CorHeader;
            Assert.IsNotNull(cor);

            var entry = cor.EntryPointTokenOrRelativeVirtualAddress;

            // 0 means "no entry point"
            Assert.AreNotEqual(0, entry);
        }
        private static void CheckMethodExists(string file, string methodName)
        {
            using var pe = new PEReader(File.OpenRead(file));
            Assert.IsTrue(pe.HasMetadata);
            var md = pe.GetMetadataReader();
            bool found = false;
            foreach (var handle in md.MethodDefinitions)
            {
                var method = md.GetMethodDefinition(handle);
                var name = md.GetString(method.Name);
                if (name == methodName)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, $"Method '{methodName}' not found.");
        }

        private static void CheckOutput(string file, string expected)
        {
            var asm = Assembly.Load(File.ReadAllBytes(file));
            var entry = asm.EntryPoint;

            // Force JIT
            RuntimeHelpers.PrepareMethod(entry!.MethodHandle);

            // Capture output
            using var sw = new StringWriter();
            Console.SetOut(sw);

            entry.Invoke(null, null);

            var output = sw.ToString();

            Assert.AreEqual(expected, output);
        }

        private static void Compile(string source, string assemblyName)
        {
            List<string> messages;

            bool success = Lexer.Lex(source, out List<IToken> result);

            messages = Parser.Parse(result, out ParseNode? twee);

            if (messages.Count > 0) throw new Exception("Did not parse.");

            messages = SemanticAnalyzer.Analyze(twee!, out var scopes);

            if (messages.Count > 0) throw new Exception("Did not pass analysis.");

            CodeGen.GenerateCode(twee!, scopes, assemblyName);
        }
        #endregion

        //cd C:\Users\test\Documents\Github\C-Flat_Compiler\Compiler\TestProject\bin\Debug\net10.0

        [TestMethod]
        public void WhileElseTest()
        {
            string program =
                "int x = 5;\n" +
                "while(x < 3)\n" +
                "{\n" +
                "    x = x + 1;\n" +
                "    print(\"Loop\");" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Done\");\n" +
                "}\n";

            string expectedOutput = "Done\r\n";

            string name = "WhileElse";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);


            program =
                "int x = 1;\n" +
                "while(x < 3)\n" +
                "{\n" +
                "    x = x + 1;\n" +
                "    print(\"Loop\");" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Done\");\n" +
                "}\n";

            expectedOutput = "Loop\r\nLoop\r\n";

            name = "WhileElse2";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void FunctionTest()
        {
            string program =
                "void Foo()\r\n" +
                "{\r\n" +
                    "\tprint(\"Bar\");\r\n" +
                "}\r\n" +
                "\r\n" +
                "int x = 5;\r\n" +
                "Foo();";

            string expectedOutput = "Bar\r\n";

            string name = "Function";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckMethodExists($"{name}.exe", "Foo");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void ForLoopTest()
        {
            string program =
                "for(int i = 0; i < 14; i++)\r\n" +
                "{\r\n" +
                "\tprint(i);\r\n" +
                "\tprint(\"Hello\");\r\n" +
                "}\r\n" +
                "\r\n" +
                "int a = 2;\r\n" +
                "for(a = 0; a < 14; a = a + 1)\r\n" +
                "{\r\n" +
                "\tprint(a);\r\n" +
                "}";

            string expectedOutput =
                "0\r\n" +
                "Hello\r\n" +
                "1\r\n" +
                "Hello\r\n" +
                "2\r\n" +
                "Hello\r\n" +
                "3\r\n" +
                "Hello\r\n" +
                "4\r\n" +
                "Hello\r\n" +
                "5\r\n" +
                "Hello\r\n" +
                "6\r\n" +
                "Hello\r\n" +
                "7\r\n" +
                "Hello\r\n" +
                "8\r\n" +
                "Hello\r\n" +
                "9\r\n" +
                "Hello\r\n" +
                "10\r\n" +
                "Hello\r\n" +
                "11\r\n" +
                "Hello\r\n" +
                "12\r\n" +
                "Hello\r\n" +
                "13\r\n" +
                "Hello\r\n" +
                "0\r\n" +
                "1\r\n" +
                "2\r\n" +
                "3\r\n" +
                "4\r\n" +
                "5\r\n" +
                "6\r\n" +
                "7\r\n" +
                "8\r\n" +
                "9\r\n" +
                "10\r\n" +
                "11\r\n" +
                "12\r\n" +
                "13\r\n";

            string name = "ForLoop";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void GotoTest()
        {
            string program =
                "int i = 0;\r\n" +
                "LoopLabel:\r\n" +
                "print(\"Loopy\");\r\n" +
                "if(i > 5)\r\n" +
                "{\r\n" +
                    "\tgoto EndLoop;\r\n" +
                "}\r\n" +
                "i = i + 1;\r\n" +
                "goto LoopLabel;\r\n" +
                "\r\n" +
                "EndLoop:\r\n" +
                "print(\"Done\");";

            string expectedOutput =
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Done\r\n";

            string name = "Goto";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void IfTest()
        {
            string program =
                "int a = 200;\r\n" +
                "\r\n" +
                "if(a < 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hi\");\r\n" +
                "}\r\n" +
                "else\r\n" +
                "{\r\n" +
                    "\tprint(\"Sup\");\r\n" +
                "}";

            string expectedOutput =
                "Sup\r\n";

            string name = "If";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void IfElseTest()
        {
            string program =
                "int a = 200;\r\n" +
                "\r\n" +
                "if(a <= 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hi\");\r\n" +
                "}\r\n" +
                "else if(a < 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hey\");\r\n" +
                "}\r\n" +
                "else\r\n" +
                "{\r\n" +
                    "\tprint(\"Sup\");\r\n" +
                "}\r\n" +
                "\r\n" +
                "ifn't(a <= 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hello\");\r\n" +
                "}\r\n" +
                "else ifn't(a != 2)\r\n" +
                "{\r\n" +
                    "\tprint(\"Yay\");\r\n" +
                    "\tprint(a);\r\n" +
                "}";

            string expectedOutput =
                "Sup\r\n" +
                "Hello\r\n";

            string name = "IfElse";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void GuessingGameTest()
        {
            string program =
                "int answer = 37;\r\n" +
                "\r\n" +
                "int min = 0;\r\n" +
                "int max = 100;\r\n" +
                "bool notDone = true;\r\n" +
                "while(notDone)\r\n" +
                "{\r\n" +
                    "\tint guess = (min + max) / 2;\r\n" +
                    "\tprint(guess);\r\n" +
                    "\tif(guess < answer)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(\"Too low\");\r\n" +
                        "\t\tmin = guess;\r\n" +
                    "\t}\r\n" +
                    "\tif(guess > answer)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(\"Too high\");\r\n" +
                        "\t\tmax = guess;\r\n" +
                    "\t}\r\n" +
                    "\tif(guess =? answer)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(\"Correct!\");\r\n" +
                        "\t\tnotDone = false;\r\n" +
                    "\t}\r\n" +
                    "\tint a = 4;\r\n" +
                "}\r\nprint(\"Goodbye\");";

            string expectedOutput =
                "50\r\n" +
                "Too high\r\n" +
                "25\r\n" +
                "Too low\r\n" +
                "37\r\n" +
                "Correct!\r\n" +
                "Goodbye\r\n";

            string name = "GuessingGame";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void IncrementerTest()
        {
            string program =
                "int a = 3;\r\n" +
                "int b = a++;\r\n" +
                "int c = --b;\r\n" +
                "int d = ++a + 2;\r\n" +
                "int e = d;\r\n" +
                "a++;\r\n" +
                "print(a);\r\n" +
                "print(b);\r\n" +
                "print(c);\r\n" +
                "print(d);\r\n" +
                "print(e);\r\n";

            string expectedOutput =
                "6\r\n" +
                "2\r\n" +
                "2\r\n" +
                "7\r\n" +
                "7\r\n";

            string name = "Incrementer";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void ClassFieldIncrementerTest()
        {
            string program =
                "class Cat\r\n" +
                "{\r\n" +
                "   int a;\r\n" +
                "   int b;\r\n" +
                "   int c;\r\n" +
                "   int d;\r\n" +
                "   int e;\r\n" +
                "}\r\n" +
                "Cat bob = new Cat();\r\n" +
                "bob.a = 3;\r\n" +
                "bob.b = bob.a++;\r\n" +
                "bob.c = --bob.b;\r\n" +
                "bob.d = ++bob.a + 2;\r\n" +
                "bob.e = bob.d;\r\n" +
                "bob.a++;\r\n" +
                "print(bob.a);\r\n" +
                "print(bob.b);\r\n" +
                "print(bob.c);\r\n" +
                "print(bob.d);\r\n" +
                "print(bob.e);\r\n";

            string expectedOutput =
                "6\r\n" +
                "2\r\n" +
                "2\r\n" +
                "7\r\n" +
                "7\r\n";

            string name = "Incrementer";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void FunctionParamTest()
        {
            string program =
                "void Foo(int a, string b)\r\n" +
                "{\r\n" +
                    "\tfor(string i = 0; i < a; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(b);\r\n" +
                    "\t}\r\n" +
                "}\r\n" +
                "\r\n" +
                "Foo(4, \"Hello\");\r\n";

            string expectedOutput =
                "Hello\r\n" +
                "Hello\r\n" +
                "Hello\r\n" +
                "Hello\r\n";

            string name = "FunctionParam";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckMethodExists($"{name}.exe", "Foo");
            CheckOutput($"{name}.exe", expectedOutput);
        }


        [TestMethod]
        public void FunctionReturnTest()
        {
            string program =
                "int Pow(int a, int b)\r\n" +
                "{\r\n" +
                    "\tint result = 1;\r\n" +
                    "\tfor(int i = 0; i < b; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tresult = result * a;\r\n" +
                    "\t}\r\n" +
                    "\treturn result;\r\n" +
                "}\r\n" +
                "\r\n" +
                //"print(Pow(2, 3));\r\n";
                "int a = Pow(2, 3);\r\n" +
                "print(a);";

            string expectedOutput =
                "8\r\n";

            string name = "FunctionReturn";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckMethodExists($"{name}.exe", "Pow");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void ClassTest()
        {
            string program =
                "Cat bob = new Cat();\r\n" +
                "bob.Name = \"Bob\";\r\n" +
                "print(bob.Name);\r\n" +
                "print(bob.Age);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                "    string Name;\r\n" +
                "    int Age;\r\n" +
                "    \r\n" +
                "    void SetName(string name)\r\n" +
                "    {\r\n" +
                "        Name = name;\r\n" +
                "    }\r\n" +
                "    int SetAge(int age)\r\n" +
                "    {\r\n" +
                "        Age = age;\r\n" +
                "        return age;\r\n" +
                "    }\r\n" +
                "}";

            string expectedOutput =
                "Bob\r\n" +
                "0\r\n";

            string name = "Class";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        public void ConstructorTest()
        {
            string program =
                "Cat bob = new Cat(\"Bob\", 2);\r\n" +
                "bob.Age = 3;\r\n" +
                "print(bob.Name);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                    "\tstring Name;\r\n" +
                    "\tint Age;\r\n" +
                    "\t\r\n" +
                    "\tCat(string name, int age)\r\n" +
                    "\t{\r\n" +
                        "\t\tName = name;\r\n" +
                        "\t\tAge = age;\r\n" +
                    "\t}\r\n" +
                "}";

            string expectedOutput =
                "Bob\r\n";

            string name = "Constructor";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }
    }
}