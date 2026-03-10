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
    public static class ErrorWriter
    {
        public static List<string> Messages { get; private set; } = [];

        private static Stack<(int Row, int Column)> Positions = [];

        public static void Move(int row, int column)
        {
            Positions.Push((row, column));
        }
        public static void Move((int row, int column) location)
        {
            Positions.Push(location);
        }
        public static void MoveBack()
        {
            Positions.Pop();
        }

        public static void Add(string message)
        {
            var location = Positions.Peek();
            Messages.Add($"{message} (Row {location.Row}, Column {location.Column})");
        }

        public static void Reset()
        {
            Messages.Clear();
            Positions.Clear();
        }
    }

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
        public List<Function> Constructors;
        public string Name;
        public ClassInfo(string name)
        {
            Fields = [];
            Methods = [];
            Constructors = [];
            Name = name;
        }

        public void AddMethod(string Name, List<VarInfo> Parameters, string ReturnType)
        {
            Function func = new Function(Name, Parameters, ReturnType, this);
            if (Methods.Contains(func))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"Method '{func.Name}' already exists in class '{Name}' with parameters: ");
                foreach (var param in func.Parameters)
                {
                    sb.Append($"{param.Type}, ");
                }
                if (sb[^1] == ' ')
                {
                    sb.Remove(sb.Length - 2, 2); //remove last comma and space
                    sb.Append('.');
                }
                ErrorWriter.Add(sb.ToString());
            }
            else
            {
                Methods.Add(func);
            }
        }
        public void AddConstructor(List<VarInfo> Parameters)
        {
            Function func = new Function($"{Name} constructor", Parameters, this.Name, this);
            if (Constructors.Contains(func))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"Constructor already exists in class '{Name}' with parameters: ");
                foreach (var param in func.Parameters)
                {
                    sb.Append($"{param.Type}, ");
                }
                if (sb[^1] == ' ')
                {
                    sb.Remove(sb.Length - 2, 2); //remove last comma and space
                    sb.Append('.');
                }
                ErrorWriter.Add(sb.ToString());
            }
            else
            {
                Constructors.Add(func);
            }
        }
        public void CheckMethodCall(string name, List<VarInfo> parameters)
        {
            Function? method = Methods.Find(x => x.Name == name && x.Parameters.SequenceEqual(parameters));
            if (method is null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"No method '{name}' found in class '{Name}' with parameters: ");
                foreach (var param in parameters)
                {
                    sb.Append($"{param.Type}, ");
                }
                if (sb[^1] == ' ')
                {
                    sb.Remove(sb.Length - 2, 2); //remove last comma and space
                    sb.Append('.');
                }
                ErrorWriter.Add(sb.ToString());
            }
        }
        public void CheckConstructorCall(List<VarInfo> parameters)
        {
            Function? method = Methods.Find(x => x.Parameters.SequenceEqual(parameters));
            if (method is null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"No constructor found in class '{Name}' with parameters: ");
                foreach (var param in parameters)
                {
                    sb.Append($"{param.Type}, ");
                }
                if (sb[^1] == ' ')
                {
                    sb.Remove(sb.Length - 2, 2); //remove last comma and space
                    sb.Append('.');
                }
                ErrorWriter.Add(sb.ToString());
            }
        }
        public void AddField(string name, VarInfo field)
        {
            if (!Fields.TryAdd(name, field))
            {
                ErrorWriter.Add($"Field '{name}' already exists in class '{Name}'.");
            }
        }

        public bool TryGetFunc(string name, List<VarInfo> parameters, [NotNullWhen(true)] out Function? function)
        {
            function = Methods.Find(x => x.Name == name && x.Parameters.SequenceEqual(parameters));
            return function != null;
        }
        public bool TryGetConstructor(List<VarInfo> parameters, [NotNullWhen(true)] out Function? function)
        {
            function = Methods.Find(x => x.Parameters.SequenceEqual(parameters));
            return function != null;
        }


        public override bool Equals(object? obj)
        {
            return obj is ClassInfo info &&
                   Fields.SequenceEqual(info.Fields) &&
                   Methods.SequenceEqual(info.Methods);
        }
        public override int GetHashCode() => base.GetHashCode();//makes the warning go away
    }
    public class ScopeStack
    {
        private Dictionary<string, ClassInfo> classes = [];

        public ClassInfo currClass { get; private set; }
        public Function currFunc { get; private set; }

        public ScopeStack()
        {
            currClass = new ClassInfo("Program");//default, Program class
            currFunc = new Function("Main", [], "void", currClass);//we are in Main by default, top level statement style
        }

        public void DeclareClass(ClassInfo classInfo)
        {
            if (!classes.TryAdd(classInfo.Name, classInfo))
            {
                ErrorWriter.Add($"Class '{classInfo.Name}' already exists.");
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
        public void ChangeMethod(string name, List<VarInfo> parameters)
        {
            currFunc = currClass.Methods.Find(x => x.Name == name && x.Parameters.SequenceEqual(parameters))!;
        }
        public void ChangeMethod(List<VarInfo> parameters)
        {
            currFunc = currClass.Constructors.Find(x => x.Parameters.SequenceEqual(parameters))!;
        }

        public void CheckMethodCall(string name, List<VarInfo> parameters, ClassInfo? Class = null)
        {
            if (Class is null)
            {
                Class = currClass;
            }
            Class.CheckMethodCall(name, parameters);
        }
        public void CheckFieldAccess(string name, ClassInfo? Class = null)
        {
            if (Class is null)
            {
                Class = currClass;
            }
            if (!Class.Fields.ContainsKey(name))
            {
                ErrorWriter.Add($"No field '{name}' found in class '{Class}'.");
            }
        }

        public bool TryGetVar(string name, out VarInfo value) => currFunc.TryGetVar(name, out value);
        public bool ContainsVar(string name) => currFunc.ContainsVar(name);
        public void PushVar(string symbol, VarInfo info) => currFunc.PushVar(symbol, info);

        public bool TryGetFunc(string name, List<VarInfo> parameters, string owner, [NotNullWhen(true)] out Function? function)
        {
            if (owner == "")
            {
                if (!currClass.TryGetFunc(name, parameters, out function))
                {
                    ErrorWriter.Add($"No such function {name} in class {currClass}.");
                    return false;
                }
                return true;
            }

            if (!classes.TryGetValue(owner, out var ownerClass))
            {
                function = null;
                ErrorWriter.Add($"Class {owner} not found.");
                return false;
            }
            if (!ownerClass.TryGetFunc(name, parameters, out function))
            {
                ErrorWriter.Add($"No such function {name} in class {owner}.");
                return false;
            }
            return true;
        }
        public bool TryGetConstructor(string owner, List<VarInfo> parameters, [NotNullWhen(true)] out Function? function)
        {
            if (!classes.TryGetValue(owner, out var ownerClass))
            {
                function = null;
                ErrorWriter.Add($"Class {owner} not found.");
                return false;
            }

            if (!ownerClass.TryGetConstructor(parameters, out function))
            { 
                ErrorWriter.Add($"No such constructor in class {owner}.");
                return false;
            }
            return true;
        }


        public void PushScope() => currFunc.PushScope();
        public void PopScope() => currFunc.PopScope();
    }

    public static class SemanticAnalyzer
    {
        public static List<string> Analyze(ParseNode node, out ScopeStack scopes)
        {
            ErrorWriter.Reset();
            scopes = GetClasses(node);
            CheckFunctions(node, scopes);

            return ErrorWriter.Messages;
        }

        /// <summary>
        /// Build the scope stack with all classes, methods, and fields.
        /// This is necessary to do before checking any function bodies, because of the member access and function calls.
        /// </summary>
        /// <param name="node">Root of the entire parse tree</param>
        /// <param name="messages"></param>
        /// <returns></returns>
        private static ScopeStack GetClasses(ParseNode node)
        {
            ScopeStack scopes = new();
            ErrorWriter.Move(node.Location);

            GetClasses(scopes);

            ErrorWriter.MoveBack();
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
                    ErrorWriter.Move((child as ParseNode)!.Location);
                    if (child is FunctionDeclaration funcy)
                    {
                        scopes.currClass.AddMethod(funcy.Name, funcy.Parameters.Select(x => new VarInfo(x.Type)).ToList(), funcy.ReturnType);
                    }
                    else if (child is ConstructorDeclaration constr)
                    {
                        scopes.currClass.AddConstructor(constr.Parameters.Select(x => new VarInfo(x.Type)).ToList());
                    }
                    else if (child is VariableDeclaration vari)
                    {
                        scopes.currClass.AddField(vari.Name, new VarInfo(vari.TypeExpected));
                    }
                    else
                    {
                        ErrorWriter.Add($"Unexpected node type '{child.GetType()}' in class body.");
                    }
                    ErrorWriter.MoveBack();
                }

            }
        }


        private static void CheckFunctions(ParseNode node, ScopeStack scopes)
        {
            ErrorWriter.Move(node.Location);
            if (node is FunctionDeclaration funcy)
            {
                scopes.ChangeMethod(funcy.Name, funcy.Parameters.Select(x => new VarInfo(x.Type)).ToList());
                foreach (var child in funcy.Children)
                {
                    CheckFunctionBody((child as ParseNode)!);
                }
            }
            if (node is ConstructorDeclaration constr)
            {
                scopes.ChangeMethod(constr.Parameters.Select(x => new VarInfo(x.Type)).ToList());
                foreach (var child in constr.Children)
                {
                    CheckFunctionBody((child as ParseNode)!);
                }
            }
            else if (node is ClassDeclaration classy)
            {
                scopes.ChangeClass(classy.Name);
                foreach (var child in node.Children)
                {
                    CheckFunctions((child as ParseNode)!, scopes);
                }
                scopes.ChangeClass("Program");//reset to Program for top level statements
            }
            else
            { 
                foreach (var child in node.Children)
                {
                    CheckFunctions((child as ParseNode)!, scopes);
                }
            }

            ErrorWriter.MoveBack();

            void CheckFunctionBody(ParseNode curr)
            {
                ErrorWriter.Move(curr.Location);
                if (curr is FunctionCall call)
                {
                    scopes.CheckMethodCall(call.Name, ConvertParams(call));
                }
                else if (curr is VariableDeclaration decl)
                {
                    if (scopes.TryGetVar(decl.Name, out _))
                    {
                        ErrorWriter.Add($"Variable '{decl.Name}' already declared in scope.");
                    }
                    else
                    {
                        scopes.PushVar(decl.Name, new VarInfo(decl.Type));
                    }

                    if (decl.Children.Count > 0)
                    {
                        if (decl.Children.Count > 1) throw new Exception("VarDecl has multiple values");

                        CheckType(decl, decl.Type, scopes);
                    }
                }
                else if (curr is VariableAssignment assignment)
                {
                    if (!scopes.TryGetVar(assignment.Name, out VarInfo value))
                    {
                        ErrorWriter.Add($"Variable '{assignment.Name}' not declared in scope.");
                    }

                    CheckType(assignment, value.Type, scopes);
                }
                else if (curr is Incrementer incrementer)
                {
                    if (!scopes.TryGetVar(incrementer.Name, out VarInfo value))
                    {
                        ErrorWriter.Add($"Variable '{incrementer.Name}' not declared in scope.");
                    }
                }
                else if (curr is GotoStatement @goto)
                {
                    if (!scopes.currFunc.Labels.Contains(@goto.LabelName))
                    {
                        ErrorWriter.Add($"Label '{@goto.LabelName}' not found.");
                    }
                }
                else if (curr is ReturnStatement @return)
                {
                    if (@return.Value is null && scopes.currFunc.ReturnType != "void")
                    {
                        ErrorWriter.Add($"Return statement missing value.");
                    }
                    else if (@return.Value is not null && scopes.currFunc.ReturnType == "void")
                    {
                        ErrorWriter.Add($"Cannot return value from void function.");
                    }
                    else
                    {
                        if (@return.Value is not null)
                        {
                            CheckType(@return.Value, scopes.currFunc.ReturnType, scopes);
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

                ErrorWriter.MoveBack();
            }
        }

        private static void CheckType(ParseNode node, string type, ScopeStack scopes)
        {
            ErrorWriter.Move(node.Location);
            if (node is ASTNode ast && ast.Children.Count == 0) //is terminal
            {
                CheckTerminalType(ast, type, scopes);
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
                        ErrorWriter.Move(call.Location);
                        if (!scopes.TryGetFunc(call.Name, ConvertParams(call), call.Owner, out Function? func))
                        {
                            ErrorWriter.Add($"Function '{call.Name}' not found.");
                        }
                        else
                        {
                            //type check return
                            if (func.ReturnType != type)
                            {
                                ErrorWriter.Add($"Function '{call.Name}' returns {func.ReturnType}, not {type}.");
                            }

                            //parameters will be type checked automatically later
                        }
                        ErrorWriter.MoveBack();
                    }
                    else if (child is ClassInstantiation inst)
                    {
                        ErrorWriter.Move(inst.Location);
                        if (scopes.TryGetConstructor(inst.ClassName, ConvertParams(inst), out Function? func))
                        {
                            //type check instantiation
                            if (inst.ClassName != type)
                            {
                                ErrorWriter.Add($"Instantiated a(n) {inst.ClassName}, not {type}.");
                            }
                        }
                        ErrorWriter.MoveBack();
                    }
                    else
                    {
                        CheckType((child as ParseNode)!, type, scopes);
                    }
                }
            }
            ErrorWriter.MoveBack();
        }
        private static bool CheckTerminalType(ASTNode node, string type, ScopeStack scopes)
        {
            ErrorWriter.Move(node.Location);
            if (node.Token is Identifier id)
            {
                if (scopes.TryGetVar(id.Text, out var info))
                {
                    if (info.Type == type)
                    {
                        ErrorWriter.MoveBack();
                        return true;
                    }
                    else
                    {
                        ErrorWriter.Add($"Expected type '{type}' but found '{id.Text}'({info.Type}).");
                        ErrorWriter.MoveBack();
                        return false;
                    }
                }
            }
            else if (node.Token is NumericValue)
            {
                if (type == "int")
                {
                    ErrorWriter.MoveBack();
                    return true;
                }
                else
                {
                    ErrorWriter.Add($"Expected type '{type}' but found int literal.");
                    ErrorWriter.MoveBack();
                    return false;
                }
            }
            else if (node.Token is StringValue)
            {
                if (type == "string")
                {
                    ErrorWriter.MoveBack();
                    return true;
                }
                else
                {
                    ErrorWriter.Add($"Expected type '{type}' but found string literal.");
                    ErrorWriter.MoveBack();
                    return false;
                }
            }
            else if (node.Token is BoolLiteral or TrueKeyword or FalseKeyword)
            {
                if (type == "bool")
                {
                    ErrorWriter.MoveBack();
                    return true;
                }
                else
                {
                    ErrorWriter.Add($"Expected type '{type}' but found bool literal.");
                    ErrorWriter.MoveBack();
                    return false;
                }
            }
            ErrorWriter.Add($"Unexpected type mismatch.");
            ErrorWriter.MoveBack();
            return false;
        }

        private static List<string> GetLabels(ParseNode node, List<string> labels)
        {
            if (node is null) return labels;

            ErrorWriter.Move(node.Location);
            if (node is ASTNode ast && ast.Token is Label label)
            {
                if (labels.Contains(label.Name))
                {
                    ErrorWriter.Add($"Label '{label.Name}' already declared.");
                }
                else
                {
                    labels.Add(label.Name);
                }
            }
            foreach (var child in node.Children)
            {
                GetLabels((child as ParseNode)!, labels);
            }
            ErrorWriter.MoveBack();
            return labels;
        }

        private static List<VarInfo> ConvertParams(FunctionCall call) => call.Parameters.Select(x => new VarInfo(x.TypeExpected)).ToList();
        private static List<VarInfo> ConvertParams(ClassInstantiation call) => call.Parameters.Select(x => new VarInfo(x.TypeExpected)).ToList();
    }
}