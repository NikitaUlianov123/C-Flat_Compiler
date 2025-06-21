using Compiler;

using System.Runtime.CompilerServices;


//Excellent — now we’re truly debugging the cake. 🍰

namespace TestProject
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void PrintTest()
        {
            bool success = Lexer.Lex("print(\"Hello\");", out var result);
            Assert.IsTrue(success);

            //success = Lexer.Lex("int a = 7;", out result);
            //Assert.IsTrue(success);

            Parser.NukeWhiteSpace(result);

            var messages = Parser.Parse(result, out var tree);
            ;
            Assert.IsTrue(messages.Count == 0, string.Join("\n", messages));
        }

        [TestMethod]
        public void TestMethod1()
        {
            bool success = Lexer.Lex("int a = 3 + 4;\n" +
                                     "b = 3 + 4 * 5;\n" +
                                     "int c = (1 + 2) * 3;\n" +
                                     "int d = 4 * (7 - 2);\n" +
                                     "int e = 42;\n" +
                                     "int f = 3 + 2 * (4 - 1) / 5;\n" +
                                     "int g = 5 - 3 - 2;", out var result);
            Assert.IsTrue(success);

            //success = Lexer.Lex("int a = 7;", out result);
            //Assert.IsTrue(success);

            Parser.NukeWhiteSpace(result);

            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }
    }
}