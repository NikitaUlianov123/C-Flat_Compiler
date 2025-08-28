using Compiler.Tokens;

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
        public void Push(string symbol, VarInfo info, int scope) => scopes[scope].Add(symbol, info);

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

        public List<string> Analyze(ParseNode node, out Dictionary<string, VarInfo> symbols, out List<string> labels)
        {
            List<string> messages = [];
            List<string> Labels = GetLabels(node, messages, []);
            symbols = GetSymbols(node, messages, new(), [], Labels);
            ;
            labels = Labels;
            return messages;
        }

        public Dictionary<string, VarInfo> GetSymbols(ParseNode node, List<string> messages, ScopeStack scopes, Dictionary<string, VarInfo> symbols, List<string> labels, int scope = 0)
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
                    symbols.Add(decl.Name, new VarInfo(decl.Type));
                }

                if (decl.Children.Count > 0)
                {
                    if (decl.Children.Count > 1) throw new Exception("VarDecl has multiple values");

                    CheckType(decl, decl.Type, messages, scopes);
                }
            }
            if (node is VariableAssignment assignment)
            {
                if (!scopes.TryGetValue(assignment.Name, out VarInfo value))
                {
                    messages.Add($"Variable '{assignment.Name}' not declared in scope. {assignment.Location.row}, {assignment.Location.column}");
                }

                CheckType(assignment, value.Type, messages, scopes);
            }
            if (node is GotoStatement @goto)
            {
                string destination = (@goto.Children[0] as ASTNode)!.Token.Text;
                if (!labels.Contains(destination))
                {
                    messages.Add($"Label '{destination}' not found. {@goto.Location.row}, {@goto.Location.column}");
                }

                return symbols;
            }
            if (node is ASTNode ast)
            {
                if (ast.Token is Identifier id)
                {
                    if (!scopes.Contains(id.Text))
                    {
                        messages.Add($"Variable '{id.Text}' not declared in scope. {id.Row}, {id.Column}");
                    }
                }
            }

            if (node.TypeExpected != "")
            {
                CheckType(node, node.TypeExpected, messages, scopes);
            }

            foreach (var child in node.Children)
            {
                bool opensScope = child.GetType().GetCustomAttributes(typeof(OpensScopeAttribute), true).Length > 0;
                if (opensScope)
                {
                    scopes.PushScope();
                }

                GetSymbols((child as ParseNode)!, messages, scopes, symbols, labels);

                if (opensScope)
                {
                    scopes.PopScope();
                }
            }

            return symbols;
        }

        private void CheckType(ParseNode node, string type, List<string> messages, ScopeStack scopes)
        {
            if (node is ASTNode ast && ast.Children.Count == 0) //is terminal
            {
                if (ast.Token is Identifier id)
                {
                    if (scopes.TryGetValue(id.Text, out var info))
                    {
                        if (info.Type != type)
                        {
                            messages.Add($"Expected type '{type}' but found '{id.Text}'({info.Type}) at {id.Row}, {id.Column}");
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
                    if (type != "bool")
                    {
                        messages.Add($"Expected type '{type}' but found bool literal at {ast.Location.row}, {ast.Location.column}");
                    }
                }
            }
            else
            {
                foreach (var child in node.Children)
                {
                    if (child is ASTNode nodey && nodey.TypeExpected != "")//child token expects a certain type
                    {
                        continue;
                        //if will get type checked when we get to that part of scope checking
                        //CheckType((child as ParseNode)!, nodey.TypeExpected, messages, scopes);
                    }
                    else
                    {
                        CheckType((child as ParseNode)!, type, messages, scopes);
                    }
                }
            }
        }

        private List<string> GetLabels(ParseNode node, List<string> messages, List<string> labels)
        {
            if (node is null) return labels;

            if (node is ASTNode ast && ast.Token is Label label)
            {
                if (labels.Contains(label.Name))
                {
                    messages.Add($"Label '{label.Name}' already declared. {label.Row}, {label.Column}");
                }
                else
                {
                    labels.Add(label.Name);
                }
            }
            foreach (var child in node.Children)
            {
                GetLabels((child as ParseNode)!, messages, labels);
            }
            return labels;
        }
    }
}