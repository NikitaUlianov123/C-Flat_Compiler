using Compiler;
using Compiler.Tokens;

namespace Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool success;
            List<IToken> result;

            ParseNode? twee;

            List<string> messages;
            SemanticAnalyzer analyzer;
            //#############################


            success = Lexer.Lex("int a = 2;\n" +
                                "string b = \"Hi\";\n" +
                                "bool c = true;\n" +
                                "a = \"Bye\";\n" +
                                "a = false;\n" +
                                "b = 2;\n" +
                                "b = true;\n" +
                                "c = 14;\n" +
                                "c = \"Hello\";", out result);

            messages = Parser.Parse(result, out twee);

            LogTree(twee!, 0);

            analyzer = new SemanticAnalyzer();
            analyzer.Analyze(twee!);
        }

        private static void LogTree(ParseNode node, int depth)
        {
            Console.WriteLine(new string(' ', depth * 2) + (node is ASTNode ? node : node.GetType().Name));
            foreach (var child in node.Children)
            {
                if (child is ParseNode childNode)
                {
                    LogTree(childNode, depth + 1);
                }
                else if (child is not null )
                {
                    Console.WriteLine(new string(' ', (depth + 1) * 2) + (child is ASTNode bob ? bob : child.GetType().Name));
                }
            }
        }
    }
}
