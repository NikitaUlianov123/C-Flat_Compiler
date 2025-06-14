namespace Compiler
{
    public static class Lexer
    {
        public static List<Token> Lex(string program)
        {
            List<Token> tokens = [];

            int i = 0;
        Found:
            foreach (var Class in Definition.Regex)
            {
                foreach (var regex in Class.Value)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(program[i..], regex);
                    if (match.Success)
                    {
                        ;
                        tokens.Add(new Token(Class.Key, regex, program[i.. (i + match.Length)]));
                        i += tokens.Last().Text.Length;
                        goto Found;
                    }
                }
            }

            return tokens;
        }
    }
}