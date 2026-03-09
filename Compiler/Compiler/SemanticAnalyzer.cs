using Compiler.Tokens;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;

namespace Compiler
{
    [DebuggerDisplay("Type: {Type}")]
    public record struct VarInfo(string Type);
    public class Function(string Name, List<VarInfo> Parameters, string ReturnType, ClassInfo Owner)
    {
        public string Name = Name;
        public LinkedList<VarInfo> Parameters = new(Parameters);
        public string ReturnType = ReturnType;
        public ClassInfo Owner = Owner;

        private List<Dictionary<string, VarInfo>> variables = [];
        public List<string> Labels = [];

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

        public bool ContainsVar(string name) => variables.Any(scope => scope.ContainsKey(name))
                                                || Owner.Fields.ContainsKey(name);

        public void PushVar(string symbol, VarInfo info) => variables.Last().Add(symbol, info);

        public void PushScope()
        {
            variables.Add([]);
        }

        public void PopScope()
        {
            variables.RemoveAt(variables.Count - 1);
        }

        public override bool Equals(object? obj)
        {
            return obj is Function func &&
                   Name == func.Name &&
                   Parameters.SequenceEqual(func.Parameters) &&
                   ReturnType == func.ReturnType &&
                   Owner.Equals(func.Owner);
        }

        public override int GetHashCode() => base.GetHashCode();//makes the warning go away
    }
    public class ClassInfo
    {
        public Dictionary<string, VarInfo> Fields;
        public List<Function> Methods;
        public string Name;
        public ClassInfo(string name)
        {
            Fields = [];
            Methods = [];
            Name = name;
        }

        public override bool Equals(object? obj)
        {
            return obj is ClassInfo info &&
                   Fields.SequenceEqual(info.Fields) &&
                   Methods.SequenceEqual(info.Methods);
        }

        public bool TryGetFunc(string name, List<VarInfo> parameters, [NotNullWhen(true)] out Function? function)
        {
            function = Methods.Find(x => x.Name == name && x.Parameters.SequenceEqual(parameters));
            return function != null;
        }

        public override int GetHashCode() => base.GetHashCode();//makes the warning go away
    }
    public class ScopeStack
    {
        private Dictionary<string, ClassInfo> classes = [];

        private ClassInfo currClass;
        public Function currFunc { get; private set; }

        public List<string> Messages = [];

        public ScopeStack(List<string> messages)
        {
            Messages = messages;
            currClass = new ClassInfo("Program");//default, Program class
            currFunc = new Function("Main", [], "void", currClass);//we are in Main by default, top level statement style
        }

        public void DeclareClass(ClassInfo classInfo)
        {
            if (!classes.TryAdd(classInfo.Name, classInfo))
            {
                Messages.Add($"Class '{classInfo.Name}' already exists.");
            }
            else
            {
                currClass = classInfo;
            }
        }
        public void ChangeClass(string name)
        {
            currClass = classes[name];
        }
        public void AddMethod(string Name, List<VarInfo> Parameters, string ReturnType)
        {
            Function func = new Function(Name, Parameters, ReturnType, currClass);
            if (currClass.Methods.Contains(func))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"Method '{func.Name}' already exists in class '{currClass}' with parameters: ");
                foreach (var param in func.Parameters)
                {
                    sb.Append($"{param.Type}, ");
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
        public void ChangeMethod(string name, List<VarInfo> parameters)
        {
            currFunc = currClass.Methods.Find(x => x.Name == name && x.Parameters.SequenceEqual(parameters))!;
        }
        public void AddField(string name, VarInfo field)
        {
            if (!currClass.Fields.TryAdd(name, field))
            {
                Messages.Add($"Field '{name}' already exists in class '{currClass}'.");
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
                    sb.Append($"{param.Type}, ");
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


        public bool TryGetVar(string name, out VarInfo value) => currFunc.TryGetVar(name, out value);

        public bool ContainsVar(string name) => currFunc.ContainsVar(name);

        public void PushVar(string symbol, VarInfo info) => currFunc.PushVar(symbol, info);

        public void PushScope() => currFunc.PushScope();

        public void PopScope() => currFunc.PopScope();
        public bool TryGetFunc(string name, List<VarInfo> parameters, string owner, [NotNullWhen(true)] out Function? function)
        {
            if (owner == "")
            {
                if (!currClass.TryGetFunc(name, parameters, out function))
                {
                    Messages.Add($"No such function {name} in class {currClass}.");
                    return false;
                }
                return true;
            }

            if (!classes.TryGetValue(owner, out var ownerClass))
            {
                function = null;
                Messages.Add($"Class {owner} not found.");
                return false;
            }
            return ownerClass.TryGetFunc(name, parameters, out function);
        }
    }

    public static class SemanticAnalyzer
    {
        public static List<string> Analyze(ParseNode node, out ScopeStack scopes)
        {
            List<string> messages = [];
            scopes = GetClasses(node, messages);
            CheckFunctions(node, messages, scopes);

            return messages;
        }

        /// <summary>
        /// Build the scope stack with all classes, methods, and fields.
        /// This is necessary to do before checking any function bodies, because of the member access and function calls.
        /// </summary>
        /// <param name="node">Root of the entire parse tree</param>
        /// <param name="messages"></param>
        /// <returns></returns>
        private static ScopeStack GetClasses(ParseNode node, List<string> messages)
        {
            ScopeStack scopes = new(messages);

            GetClasses(scopes);

            return scopes;

            void GetClasses(ScopeStack scopes)
            {
                if (node is null) return;

                if (node is ClassDeclaration classy)
                {
                    scopes.DeclareClass(new ClassInfo(classy.Name));
                }
                foreach (var child in node.Children)
                {
                    if (child is FunctionDeclaration funcy)
                    {
                        scopes.AddMethod(funcy.Name, funcy.Parameters.Select(x => new VarInfo(x.Type)).ToList(), funcy.ReturnType);
                    }
                    else if (child is ConstructorDeclaration constr)
                    {
                        throw new NotImplementedException("Constructors not implemented yet");
                    }
                    else if (child is VariableDeclaration vari)
                    {
                        scopes.AddField(vari.Name, new VarInfo(vari.TypeExpected));
                    }
                    else
                    {
                        messages.Add($"Unexpected node type '{child.GetType()}' in class body. {(child as ASTNode)!.Location.row}, {(child as ASTNode)!.Location.column}");
                    }
                }

            }
        }


        private static void CheckFunctions(ParseNode node, List<string> messages, ScopeStack scopes)
        {
            if (node is FunctionDeclaration funcy)
            {
                scopes.ChangeMethod(funcy.Name, funcy.Parameters.Select(x => new VarInfo(x.Type)).ToList());
                foreach (var child in funcy.Children)
                {
                    CheckFunctionBody((child as ParseNode)!);
                }
            }
            else if (node is ClassDeclaration classy)
            {
                scopes.ChangeClass(classy.Name);
            }
            else
            { 
                foreach (var child in node.Children)
                {
                    CheckFunctions((child as ParseNode)!, messages, scopes);
                }
            }

            void CheckFunctionBody(ParseNode curr)
            {
                if (curr is FunctionCall call)
                {
                    scopes.CheckMethodCall(call.Name, ConvertParams(call));
                }
                else if (curr is VariableDeclaration decl)
                {
                    if (scopes.TryGetVar(decl.Name, out _))
                    {
                        messages.Add($"Variable '{decl.Name}' already declared in scope. {decl.Location.row}, {decl.Location.column}");
                    }
                    else
                    {
                        scopes.PushVar(decl.Name, new VarInfo(decl.Type));
                    }

                    if (decl.Children.Count > 0)
                    {
                        if (decl.Children.Count > 1) throw new Exception("VarDecl has multiple values");

                        CheckType(decl, decl.Type, messages, scopes);
                    }
                }
                else if (curr is VariableAssignment assignment)
                {
                    if (!scopes.TryGetVar(assignment.Name, out VarInfo value))
                    {
                        messages.Add($"Variable '{assignment.Name}' not declared in scope. {assignment.Location.row}, {assignment.Location.column}");
                    }

                    CheckType(assignment, value.Type, messages, scopes);
                }
                else if (curr is Incrementer incrementer)
                {
                    if (!scopes.TryGetVar(incrementer.Name, out VarInfo value))
                    {
                        messages.Add($"Variable '{incrementer.Name}' not declared in scope. {incrementer.Location.row}, {incrementer.Location.column}");
                    }
                }
                else if (curr is GotoStatement @goto)
                {
                    if (!scopes.currFunc.Labels.Contains(@goto.LabelName))
                    {
                        messages.Add($"Label '{@goto.LabelName}' not found. {@goto.Location.row}, {@goto.Location.column}");
                    }
                }
                else if (curr is ReturnStatement @return)
                {
                    if (@return.Value is null && scopes.currFunc.ReturnType != "void")
                    {
                        messages.Add($"Return statement missing value. {@return.Location.row}, {@return.Location.column}");
                    }
                    else if (@return.Value is not null && scopes.currFunc.ReturnType == "void")
                    {
                        messages.Add($"Cannot return value from void function. {@return.Location.row}, {@return.Location.column}");
                    }
                    else
                    {
                        if (@return.Value is not null)
                        {
                            CheckType(@return.Value, scopes.currFunc.ReturnType, messages, scopes);
                        }
                    }
                }
                else if (curr is ASTNode ast)
                {
                    throw new NotImplementedException("see if this ever hits");
                    //if (ast.Token is Identifier id)
                    //{
                    //    if (!scopes.ContainsVar(id.Text))
                    //    {
                    //        messages.Add($"Variable '{id.Text}' not declared in scope. {id.Row}, {id.Column}");
                    //    }
                    //}
                }
            }
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
        private static void CheckType(ParseNode node, string type, List<string> messages, ScopeStack scopes)
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
                        if (!scopes.TryGetFunc(call.Name, ConvertParams(call), call.Owner, out Function? func))
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
                        CheckType((child as ParseNode)!, type, messages, scopes);
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

        private static List<VarInfo> ConvertParams(FunctionCall call) => call.Parameters.Select(x => new VarInfo(x.TypeExpected)).ToList();
    }
}