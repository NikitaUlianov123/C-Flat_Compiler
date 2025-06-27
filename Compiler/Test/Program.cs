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

            LogTree(twee!, 0);

            if(messages.Count > 0) throw new Exception("Did not tokenize.");

            analyzer = new SemanticAnalyzer();
            messages = analyzer.Analyze(twee!);
            ;
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
