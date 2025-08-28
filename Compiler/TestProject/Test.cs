using Compiler;
using Compiler.Tokens;

using System.Runtime.CompilerServices;


//Excellent — now we’re truly debugging the cake. 🍰

namespace TestProject
{
    [TestClass]
    public sealed class Parsing
    {
        [TestMethod]
        public void PrintTest()
        {
            bool success = Lexer.Lex("print(\"Hello\");", out var result);
            Assert.IsTrue(success);

            var messages = Parser.Parse(result, out var tree);
            ;
            Assert.IsTrue(messages.Count == 0, string.Join("\n", messages));
        }

        [TestMethod]
        public void MathTest()
        {
            bool success = Lexer.Lex("int a = 3 + 4;\n" +
                                     "b = 3 + 4 * 5;\n" +
                                     "int c = (1 + 2) * 3;\n" +
                                     "int d = 4 * (7 - 2);\n" +
                                     "int e = 42;\n" +
                                     "int f = 3 + 2 * (4 - 1) / 5;\n" +
                                     "int g = 5 - 3 - 2;", out var result);
            Assert.IsTrue(success);


            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }

        [TestMethod]
        public void IfTest()
        {
            bool success = Lexer.Lex("if(a > 3 && !(b || c) || d)\n" +
                                     "{\n" +
                                     "int e = 42;\n" +
                                     "}\n", out var result);
            Assert.IsTrue(success);

            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }

        [TestMethod]
        public void ElseTest()
        {
            bool success = Lexer.Lex("int a = 200;\n" +
                                     "if (a <= 37)\n" +
                                     "{\n" +
                                     "    print(\"Hi\");\n" +
                                     "}\n" +
                                     "else if (a < 42)\n" +
                                     "{\n" +
                                     "    print(\"Hey\");\n" +
                                     "}\n" +
                                     "else\n" +
                                     "{\n" +
                                     "    print(\"Sup\");\n" +
                                     "}\n" +
                                     "\n" +
                                     "ifn't(a <= 22)\n" +
                                     "{\n" +
                                     "    print(\"Hello\");\n" +
                                     "}\n" +
                                     "else ifn't(a != 2)\n" +
                                     "{\n" +
                                     "    print(\"Yay\");\n" +
                                     "}", out var result);
            Assert.IsTrue(success);

            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }

        [TestMethod]
        public void ScopeTest()
        {
            bool success;
            List<IToken> result;

            ParseNode? twee;

            List<string> messages;
            //#############################


            success = Lexer.Lex("int a = 2;\n" +
                                "if(a > 3 && !(b || c) || !d)\n" +
                                "{\n" +
                                "int e = 42;\n" +
                                "int b;\n" +
                                "a = 14;\n" +
                                "}\n" +
                                "e = 3;", out result);

            messages = Parser.Parse(result, out twee);
        }
    }

    [TestClass]
    public sealed class Semantic
    {
        [TestMethod]
        public void TypeTest()
        {
            bool success;
            List<IToken> result;

            ParseNode? twee;

            List<string> messages;
            SemanticAnalyzer analyzer;
            //#############################


            success = Lexer.Lex(
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
                                , out result);

            messages = Parser.Parse(result, out twee);

            ;

            if (messages.Count > 0) throw new Exception("Did not tokenize.");

            analyzer = new SemanticAnalyzer();
            //messages = analyzer.Analyze(twee!);
            ;
        }
    }
}