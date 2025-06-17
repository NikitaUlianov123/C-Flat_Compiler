namespace Compiler
{
    public static class Lexer
    {
        public static List<Token> Lex(string program)
        {
            List<Token> tokens = [];

            Token.MakeToken(program);

            return tokens;
        }
    }
}