using Compiler.Tokens;

namespace Compiler
{
    public static class Lexer
    {
        public static bool Lex(string program, out List<IToken> tokens)
        {
            bool success = true;
            tokens = [];

            for (int i = 0; i < program.Length; i += tokens[^1].Text.Length)
            {
                var token = TokenFactory.MakeToken(program);
                tokens.Add(token);

                if(token is Error) success = false;
            }

            return success;
        }
    }
}