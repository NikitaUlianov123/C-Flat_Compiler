using Compiler.Tokens;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compiler.Tokens
{
    public interface IToken
    {
        List<string> Regex { get; }

        public string Match { get; }//In case I need to know what exactly matched

        //public abstract static bool DoesMatch(string input, [NotNullWhen(true)] out IToken? result);
    }

    public static class TokenFactory
    {
        private static readonly List<Type> TokenTypes =
        [
            typeof(Whitespace),
            typeof(Keyword),
            // Add more here
        ];

        public static IToken? TryMakeToken(string input)
        {
            foreach (var type in TokenTypes)
            {
                // Find: public static bool DoesMatch(string, out IToken)
                var method = type.GetMethod("DoesMatch", BindingFlags.Public | BindingFlags.Static);
                if (method == null) throw new InvalidOperationException($"{type.Name} is missing static DoesMatch method.");

                var parameters = new object?[] { input, null };

                bool success = (bool)method.Invoke(null, parameters)!;

                if (success) return (IToken)parameters[1]!;
            }

            return null;
        }
    }
}
