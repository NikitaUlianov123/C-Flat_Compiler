using Compiler.Tokens;

namespace Compiler
{
    public static class Lexer
    {
        public static bool Lex(string program, out List<IToken> tokens)
        {
            bool success = true;
            tokens = [];

            int row = 0;
            int column = 0;
            for (int i = 0; i < program.Length; i += tokens[^1].Text.Length)
            {
                var token = TokenFactory.MakeToken(program[i..], row, column);
                tokens.Add(token);

                if(token is Error) success = false;


                if (token.Text.Contains('\n'))
                {
                    row += token.Text.Count(c => c == '\n');
                    column = 0;
                }
                else
                {
                    column += token.Text.Length;
                }
            }

            return success;
        }
    }
}