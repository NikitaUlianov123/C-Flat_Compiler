using Compiler.Tokens;
using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;

namespace Compiler
{
    [DebuggerDisplay("Type: {Type}")]
    public record struct VarInfo(string type);
    public class Function(string Name, List<VarInfo> Parameters, string ReturnType)
    {
        public string Name = Name;
        public LinkedList<VarInfo> Parameters = new(Parameters);
        public string ReturnType = ReturnType;

        private List<Dictionary<string, VarInfo>> variables = [];

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

        public bool ContainsVar(string name) => variables.Any(scope => scope.ContainsKey(name));

        public void PushVar(string symbol, VarInfo info) => variables.Last().Add(symbol, info);

        public void PushScope()
        {
            variables.Add([]);
        }

        public void PopScope()
        {
            variables.RemoveAt(variables.Count - 1);
        }

        public virtual bool Equals(Function? info)
        {
            return info is not null &&
                    Name == info.Name &&
                    Parameters.SequenceEqual(info.Parameters) &&
                    ReturnType == info.ReturnType;
        }
    }
    public class ScopeStack
    {
        public class ClassInfo
        {
            public Dictionary<string, VarInfo> Fields;
            public List<Function> Methods;
            public ClassInfo()
            {
                Fields = [];
                Methods = [];
            }
        }

        private Dictionary<string, ClassInfo> classes = [];

        private ClassInfo currClass;
        private Function currFunc;

        public List<string> Messages = [];
        
        public ScopeStack(List<string> messages)
        {
            Messages = messages;
            currClass = new ClassInfo();//default, Program class
            currFunc = new Function("Main", [], "void");//we are in Main by default, top level statement style
        }

        public void DeclareClass(string name, ClassInfo classInfo)
        {
            if (!classes.TryAdd(name, classInfo))
            {
                Messages.Add($"Class '{name}' already exists.");
            }
        }

        public void AddMethod(Function func)
        {
            if (currClass.Methods.Contains(func))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"Method '{func.Name}' already exists in class '{currClass}' with parameters: ");
                foreach (var param in func.Parameters)
                {
                    sb.Append($"{param.type}, ");
                }
                if (sb[^1] == ' ')
                {
                    sb.Remove(sb.Length - 2, 2); //remove last comma and space
                    sb.Append('.');
                }
                Messages.Add(sb.ToString());
            }
            else
            {
                currClass.Methods.Add(func);
            }
        }
        public void AddField(string name, VarInfo field)
        {
            if (currClass.Fields.ContainsKey(name))
            {
                Messages.Add($"Field '{name}' already exists in class '{currClass}'.");
            }
            else
            {
                currClass.Fields.Add(name, field);
            }
        }

        public void CheckMethodCall(string name, List<VarInfo> parameters, ClassInfo? Class = null)
        {
            if (Class is null)
            {
                Class = currClass;
            }

            Function? method = Class.Methods.Find(x => x.Name == name && x.Parameters.SequenceEqual(parameters));
            if (method is null)
            { 
                StringBuilder sb = new StringBuilder();
                sb.Append($"No method '{name}' found in class '{Class}' with parameters: ");
                foreach (var param in parameters)
                {
                    sb.Append($"{param.type}, ");
                }
                if (sb[^1] == ' ')
                { 
                    sb.Remove(sb.Length - 2, 2); //remove last comma and space
                    sb.Append('.');
                }
                Messages.Add(sb.ToString());
            }
        }

        public void CheckFieldAccess(string name, ClassInfo? Class = null)
        {
            if (Class is null)
            {
                Class = currClass;
            }
            if (!Class.Fields.ContainsKey(name))
            {
                Messages.Add($"No field '{name}' found in class '{Class}'.");
            }
        }
    }

    public static class SemanticAnalyzer
    {
        public static List<string> Analyze(ParseNode node, out Dictionary<string, VarInfo> symbols, out List<string> labels)
        {
            List<string> messages = [];
            List<string> Labels = GetLabels(node, messages, []);
            symbols = GetSymbols(node, messages, new(), [], Labels, default);
            ;
            labels = Labels;
            return messages;
        }

        public static Dictionary<string, VarInfo> GetSymbols(ParseNode node,
                                                             List<string> messages,
                                                             ScopeStack scopes,
                                                             Dictionary<string, VarInfo> symbols,
                                                             List<string> labels,
                                                             FuncInfo currFunc)
        {
            if (node is FunctionDeclaration funcy)
            {
                if (scopes.TryGetFunc(funcy.Name, out _))
                {
                    messages.Add($"Function '{funcy.Name}' already declared in scope. {funcy.Location.row}, {funcy.Location.column}");
                }
                else
                {
                    FuncInfo newFunc = new(funcy.ReturnType, funcy.Parameters.Select(x => new VarInfo(x.Type)).ToList());
                    scopes.PushFunc(funcy.Name, newFunc);
                    foreach (FunctionParameter param in funcy.Parameters)
                    {
                        scopes.PushVar(param.Name, new VarInfo(param.Type));
                    }
                    //Set current function for return type checking
                    currFunc = newFunc;
                }
            }
            else if (node is FunctionCall call)
            {
                if (!scopes.TryGetFunc(call.Name, out FuncInfo func))
                {
                    messages.Add($"Function '{call.Name}' not found. {call.Location.row}, {call.Location.column}");
                }
                else
                {
                    //Type check parameters
                    if (call.Parameters.Count != func.Parameters.Count)
                    {
                        messages.Add($"Incorrect number of parameters for {call.Name}. {call.Location.row}, {call.Location.column}");
                    }
                    else
                    {
                        for (int i = 0; i < func.Parameters.Count; i++)
                        {
                            if (!CheckTerminalType(call.Parameters[i], func.Parameters[i].Type, messages, scopes))
                            {
                                messages.Add($"Incorrect type for parameter {i}. {call.Location.row}, {call.Location.column}");
                            }
                        }
                    }

                    return symbols;
                }
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

                    CheckType(decl, decl.Type, messages, scopes, currFunc);
                }
            }
            else if (node is VariableAssignment assignment)
            {
                if (!scopes.TryGetVar(assignment.Name, out VarInfo value))
                {
                    messages.Add($"Variable '{assignment.Name}' not declared in scope. {assignment.Location.row}, {assignment.Location.column}");
                }

                CheckType(assignment, value.Type, messages, scopes, currFunc);
            }
            else if (node is Incrementer incrementer)
            {
                if (!scopes.TryGetVar(incrementer.Name, out VarInfo value))
                {
                    messages.Add($"Variable '{incrementer.Name}' not declared in scope. {incrementer.Location.row}, {incrementer.Location.column}");
                }
            }
            else if (node is GotoStatement @goto)
            {
                if (!labels.Contains(@goto.LabelName))
                {
                    messages.Add($"Label '{@goto.LabelName}' not found. {@goto.Location.row}, {@goto.Location.column}");
                }
            }
            else if (node is ReturnStatement @return)
            {
                if (currFunc.Equals(default))
                {
                    messages.Add($"Return statement used outside of function. {@return.Location.row}, {@return.Location.column}");
                }
                else
                {
                    if (@return.Value is null && currFunc.ReturnType != "void")
                    {
                        messages.Add($"Return statement missing value. {@return.Location.row}, {@return.Location.column}");
                    }
                    else if (@return.Value is not null && currFunc.ReturnType == "void")
                    {
                        messages.Add($"Cannot return value from void function. {@return.Location.row}, {@return.Location.column}");
                    }
                    else
                    {
                        if (@return.Value is not null)
                        {
                            CheckType(@return.Value, currFunc.ReturnType, messages, scopes, currFunc);
                        }
                    }
                }
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
                CheckType(node, node.TypeExpected, messages, scopes, currFunc);
            }

            foreach (var child in node.Children)
            {
                bool opensScope = child.GetType().GetCustomAttributes(typeof(OpensScopeAttribute), true).Length > 0;
                if (opensScope)
                {
                    scopes.PushScope();
                }

                GetSymbols((child as ParseNode)!, messages, scopes, symbols, labels, currFunc);

                if (opensScope)
                {
                    scopes.PopScope();
                }
            }

            return symbols;
        }

        private static bool CheckTerminalType(ASTNode node, string type, List<string> messages, ScopeStack scopes)
        {
            if (node.Token is Identifier id)
            {
                if (scopes.TryGetVar(id.Text, out var info))
                {
                    if (info.Type == type)
                    {
                        return true;
                    }
                    else
                    {
                        messages.Add($"Expected type '{type}' but found '{id.Text}'({info.Type}) at {id.Row}, {id.Column}");
                        return false;
                    }
                }
            }
            else if (node.Token is NumericValue)
            {
                if (type == "int")
                {
                    return true;
                }
                else
                {
                    messages.Add($"Expected type '{type}' but found int literal at {node.Location.row}, {node.Location.column}");
                    return false;
                }
            }
            else if (node.Token is StringValue)
            {
                if (type == "string")
                {
                    return true;
                }
                else
                {
                    messages.Add($"Expected type '{type}' but found string literal at {node.Location.row}, {node.Location.column}");
                    return false;
                }
            }
            else if (node.Token is BoolLiteral or TrueKeyword or FalseKeyword)
            {
                if (type == "bool")
                {
                    return true;
                }
                else
                {
                    messages.Add($"Expected type '{type}' but found bool literal at {node.Location.row}, {node.Location.column}");
                    return false;
                }
            }
            messages.Add($"Unexpected type mismatch at {node.Token.Row}, {node.Token.Column}");
            return false;
        }
        private static void CheckType(ParseNode node, string type, List<string> messages, ScopeStack scopes, FuncInfo currFunc)
        {
            if (node is ASTNode ast && ast.Children.Count == 0) //is terminal
            {
                CheckTerminalType(ast, type, messages, scopes);
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
                    else if (child is FunctionCall call)
                    {
                        if (!scopes.TryGetFunc(call.Name, out FuncInfo func))
                        {
                            messages.Add($"Function '{call.Name}' not found. {call.Location.row}, {call.Location.column}");
                        }
                        else
                        {
                            //type check return
                            if (func.ReturnType != type)
                            {
                                messages.Add($"Function '{call.Name}' returns {func.ReturnType}, not {type}. {call.Location.row}, {call.Location.column}");
                            }

                            //parameters will be type checked automatically later
                        }
                    }
                    else
                    {
                        CheckType((child as ParseNode)!, type, messages, scopes, currFunc);
                    }
                }
            }
        }

        private static List<string> GetLabels(ParseNode node, List<string> messages, List<string> labels)
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