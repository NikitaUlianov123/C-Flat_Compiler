using Compiler.Tokens;

namespace Compiler
{
    public struct VarInfo
    {
        public string Type;
        public VarInfo(string type)
        {
            Type = type;
        }
    }
    public struct FuncInfo
    {
        public string ReturnType;
        public string Name;
        public List<VarInfo> Parameters;

        public FuncInfo(string returnType, string name, List<VarInfo> parameters)
        {
            ReturnType = returnType;
            Name = name;
            Parameters = parameters;
        }
    }
    public class ScopeStack
    {
        private List<Dictionary<string, VarInfo>> variables;
        private List<Dictionary<string, FuncInfo>> functions;

        public ScopeStack()
        {
            variables = [[]];
            functions = [[]];
        }

        public bool TryGetVar(string name, out VarInfo value)
        {
            for (int i = variables.Count - 1; i >= 0; i--)
            {
                if (variables[i].TryGetValue(name, out value))
                {
                    return true;
                }
            }
            value = default;
            return false;
        }

        public bool TryGetFunc(string name, out FuncInfo value)
        {
            for (int i = functions.Count - 1; i >= 0; i--)
            {
                if (functions[i].TryGetValue(name, out value))
                {
                    return true;
                }
            }
            value = default;
            return false;
        }

        public bool ContainsVar(string name) => variables.Any(scope => scope.ContainsKey(name));
        public bool ContainsFunc(string name) => functions.Any(scope => scope.ContainsKey(name));

        public void PushVar(string symbol, VarInfo info) => variables.Last().Add(symbol, info);
        public void PushVar(string symbol, VarInfo info, int scope) => variables[scope].Add(symbol, info);

        public void PushFunc(string symbol, FuncInfo info) => functions.Last().Add(symbol, info);
        public void Pushfunc(string symbol, FuncInfo info, int scope) => functions[scope].Add(symbol, info);

        public void PushScope()
        {
            variables.Add([]);
            functions.Add([]);
        }

        public void PopScope()
        {
            variables.RemoveAt(variables.Count - 1);
            functions.RemoveAt(functions.Count - 1);
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
            if (node is FunctionDeclaration funcy)
            {
                if (scopes.TryGetFunc(funcy.Name, out _))
                {
                    messages.Add($"Variable '{funcy.Name}' already declared in scope. {funcy.Location.row}, {funcy.Location.column}");
                }
                else
                {
                    scopes.PushVar(funcy.Name, new VarInfo(funcy.ReturnType));
                    symbols.Add(funcy.Name, new VarInfo(funcy.ReturnType));
                }

                //Todo: check return type
            }
            else if (node is VariableDeclaration decl)
            {
                if (scopes.TryGetVar(decl.Name, out _))
                {
                    messages.Add($"Variable '{decl.Name}' already declared in scope. {decl.Location.row}, {decl.Location.column}");
                }
                else
                {
                    scopes.PushVar(decl.Name, new VarInfo(decl.Type));
                    symbols.Add(decl.Name, new VarInfo(decl.Type));
                }

                if (decl.Children.Count > 0)
                {
                    if (decl.Children.Count > 1) throw new Exception("VarDecl has multiple values");

                    CheckType(decl, decl.Type, messages, scopes);
                }
            }
            else if (node is VariableAssignment assignment)
            {
                if (!scopes.TryGetVar(assignment.Name, out VarInfo value))
                {
                    messages.Add($"Variable '{assignment.Name}' not declared in scope. {assignment.Location.row}, {assignment.Location.column}");
                }

                CheckType(assignment, value.Type, messages, scopes);
            }
            else if (node is GotoStatement @goto)
            {
                if (!labels.Contains(@goto.LabelName))
                {
                    messages.Add($"Label '{@goto.LabelName}' not found. {@goto.Location.row}, {@goto.Location.column}");
                }
            }
            else if (node is FunctionCall call)
            { 
                
            }
            else if (node is ASTNode ast)
            {
                if (ast.Token is Identifier id)
                {
                    if (!scopes.ContainsVar(id.Text))
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
                    if (scopes.TryGetVar(id.Text, out var info))
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