using Compiler;

using System.Runtime.CompilerServices;


//Excellent — now we’re truly debugging the cake. 🍰

namespace TestProject
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void TestMethod1()
        {
            bool success = Lexer.Lex("print(\"Hello\");\nint a = 2;\nb = ;", out var result);
            Assert.IsTrue(success);

            //success = Lexer.Lex("int a = 7;", out result);
            //Assert.IsTrue(success);

            Parser.NukeWhiteSpace(result);

            var parseTree = Parser.Parse(result, out var tree);
            ;
        }
    }
}