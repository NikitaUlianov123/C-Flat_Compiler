using Compiler;

namespace Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool success = Lexer.Lex("print(\"hello\");\n" +
                                     "print(bob);", out var result);


            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);

            LogTree(twee, 0);


            ;

            success = Lexer.Lex("int a = 3;\n" +
                                "b = 4;\n", out result);

            messages = Parser.Parse(result, out twee);

            LogTree(twee, 0);

            ;

            success = Lexer.Lex("int a = 3 + 4;\n" +
                                "b = 3 + 4 * 5;\n" +
                                "int c = (1 + 2) * 3;\n" +
                                "int d = 4 * (7 - 2);\n" +
                                "int e = 42;\n" +
                                "int f = 3 + 2 * (4 - 1) / 5;\n" +
                                "int g = 5 - 3 - 2;", out result);

            messages = Parser.Parse(result, out twee);

            LogTree(twee, 0);
        }

        private static void LogTree(ParseNode node, int depth)
        {
            Console.WriteLine(new string(' ', depth * 2) + node.GetType().Name);
            foreach (var child in node.Children)
            {
                if (child is ParseNode childNode)
                {
                    LogTree(childNode, depth + 1);
                }
                else if (child is not null )
                {
                    Console.WriteLine(new string(' ', (depth + 1) * 2) + child.GetType().Name);
                }
            }
        }
    }
}
