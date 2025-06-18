using Compiler.Tokens;

namespace Compiler
{
    public static class Lexer
    {
        public static List<IToken> Lex(string program)
        {
            List<IToken> tokens = [];

            for (int i = 0; i < program.Length; i += tokens[^1].Match.Length)
            {
                var Token = TokenFactory.TryMakeToken(program);

                if (Token is null)
                {
                    if (tokens[^1] is Error prev)
                    {
                        i -= prev.Match.Length;//so i increments later to the correct value
                        prev.Append(program[i].ToString());
                    }
                    else
                    {
                        tokens.Add(new Error(program[i].ToString()));
                    }
                }
                else
                { 
                    tokens.Add(Token);
                }
            }
            return tokens;
        }
    }
}