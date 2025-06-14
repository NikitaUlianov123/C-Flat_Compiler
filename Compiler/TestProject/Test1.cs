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
            var result = Lexer.Lex("print(\"Hello\");");
            ;

            result = Lexer.Lex("int a = 7;");
            ;
        }
    }
}