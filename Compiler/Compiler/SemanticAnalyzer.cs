using Compiler.Tokens;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Compiler
{
    public class ScopeStack
    {
        private List<Dictionary<string, VarInfo>> scopes;

        public ScopeStack()
        {
            scopes = [[]];
        }

        public bool TryGetValue(string name, out VarInfo value)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].TryGetValue(name, out value))
                {
                    return true;
                }
            }
            value = default;
            return false;
        }

        public bool Contains(string name)
        {
            return scopes.Any(scope => scope.ContainsKey(name));
        }

        public void Push(string symbol, VarInfo info) => scopes.Last().Add(symbol, info);

        public void PushScope() => scopes.Add([]);

        public void PopScope() => scopes.RemoveAt(scopes.Count - 1);

    }
    public struct VarInfo
    {
        public string Type;
        public VarInfo(string type)
        {
            Type = type;
        }
    }

    public class SemanticAnalyzer
    {
        public SemanticAnalyzer()
        {
        }

        public void Analyze(ParseNode node)
        {
            List<string> messages = [];
            GetSymbols(node, messages, new());
            ;
        }

        public void GetSymbols(ParseNode node, List<string> messages, ScopeStack scopes)
        {
            if (node is VariableDeclaration decl)
            {
                if (scopes.TryGetValue(decl.Name, out VarInfo value))
                {
                    messages.Add($"Variable '{decl.Name}' already declared in scope. {decl.Location.row}, {decl.Location.column}");
                }
                else
                {
                    scopes.Push(decl.Name, new VarInfo(decl.Type));
                }

                if (decl.Children.Count > 0)
                {
                    if (decl.Children.Count > 1) throw new Exception("VarDecl has multiple values");


                }
            }
            if (node is VariableAssignment assignment)
            {
                if (!scopes.Contains(assignment.Name))
                {
                    messages.Add($"Variable '{assignment.Name}' not declared in scope. {assignment.Location.row}, {assignment.Location.column}");
                }
            }
            if (node is ASTNode ast && ast.Token is Identifier id)
            {
                if (!scopes.Contains(id.Text))
                {
                    messages.Add($"Variable '{id.Text}' not declared in scope. {id.Row}, {id.Column}");
                }
            }

            foreach (var child in node.Children)
            {
                bool opensScope = child.GetType().GetCustomAttributes(typeof(OpensScopeAttribute), true).Length > 0;
                if (opensScope)
                {
                    scopes.PushScope();
                }

                GetSymbols((child as ParseNode)!, messages, scopes);

                if (opensScope)
                {
                    scopes.PopScope();
                }
            }
        }

        private void CheckType(ParseNode node, string type, List<string> messages, ScopeStack scopes)
        {
            if (node is ASTNode ast && ast.Children.Count == 0)
            {
                if (ast.Token is Identifier id)
                {
                    if (scopes.TryGetValue(id.Text, out var info))
                    {
                        if (info.Type != type)
                        {
                            messages.Add($"Expected type '{type}' but found '{id.Text}' at {id.Row}, {id.Column}");
                        }
                    }
                }
                else if (ast.Token is NumericValue)
                {
                    if (type != "int")
                    {
                        messages.Add($"Expected type '{type}' but found int literal at {ast.Location.row}, {ast.Location.column}");
                    }
                }
                else if (ast.Token is StringValue)
                {
                    if (type != "string")
                    {
                        messages.Add($"Expected type '{type}' but found string literal at {ast.Location.row}, {ast.Location.column}");
                    }
                }
                else if (ast.Token is BoolLiteral or TrueKeyword or FalseKeyword)
                {
                    if (type != "string")
                    {
                        messages.Add($"Expected type '{type}' but found bool literal at {ast.Location.row}, {ast.Location.column}");
                    }
                }
            }
            else
            {
                foreach (var child in node.Children)
                {
                    CheckType((child as ParseNode)!, type, messages, scopes);
                }
            }
        }
    }
}