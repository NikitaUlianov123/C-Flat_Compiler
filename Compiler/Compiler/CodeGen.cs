using Compiler.Tokens;

using Microsoft.VisualBasic;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Compiler
{
    public static class CodeGen
    {
        private static readonly Dictionary<Type, OpCode> Comparisons = new()
        {
            [typeof(LessThanOperator)] = OpCodes.Clt,
            [typeof(GreaterThanOperator)] = OpCodes.Cgt,
            [typeof(EqualityOperator)] = OpCodes.Ceq,
        };
        private static readonly Dictionary<Type, OpCode> AnnoyingComparisons = new()
        {
            [typeof(LessThanOrEqualOperator)] = OpCodes.Cgt,
            [typeof(GreaterThanOrEqualOperator)] = OpCodes.Clt,
            [typeof(NotEqualityOperator)] = OpCodes.Ceq,
        };

        private static readonly Dictionary<Type, OpCode> MathOperators = new()
        {
            [typeof(PlusOperator)] = OpCodes.Add,
            [typeof(MinusOperator)] = OpCodes.Sub,
            [typeof(TimesOperator)] = OpCodes.Mul,
            [typeof(DivideOperator)] = OpCodes.Div,
        };

        private static readonly Dictionary<Type, OpCode> BoolOperators = new()
        {
            [typeof(AndOperator)] = OpCodes.And,
            [typeof(OrOperator)] = OpCodes.Or,
        };

        private static Dictionary<string, Type> TypeMap;

        private static readonly Dictionary<string, Type> BaseTypes = new()
        {
            ["int"] = typeof(int),
            ["string"] = typeof(string),
            ["bool"] = typeof(bool),
            ["float"] = typeof(float),
            ["double"] = typeof(double),
            ["char"] = typeof(char),
            ["object"] = typeof(object),
            ["long"] = typeof(long),
            ["short"] = typeof(short),
            ["byte"] = typeof(byte),
            ["void"] = typeof(void),
        };

        private class ClassBuilder
        {
            public ClassInfo ClassInfo;
            public TypeBuilder TypeBuilder { get; init; }
            public Dictionary<Function, MethodBuilder> Methods { get; init; } = [];

            public ClassBuilder(ClassInfo classInfo, TypeBuilder typeBuilder)
            {
                ClassInfo = classInfo;
                TypeBuilder = typeBuilder;
            }
        }

        private static Dictionary<string, ClassBuilder> classBuilders = [];

        private static Dictionary<string, System.Reflection.Emit.Label> MainLabels = [];
        private static Dictionary<string, int> MainLocals = [];
        private static Dictionary<string, Type> MainSymbols = [];

        public static string GenerateCode(ParseNode tree,
                                          ScopeStack scopes,
                                          string assemblyName = "EmittedProgram")
        {
            classBuilders = [];
            MainLabels = [];
            MainLocals = [];
            MainSymbols = [];
            TypeMap = BaseTypes.Select(x => x).ToDictionary(k => k.Key, v => v.Value);//Dictionary.Copy() yooo!


            string fileName = assemblyName + ".exe";

            // Define the assembly and module
            var name = new AssemblyName(assemblyName);
            PersistedAssemblyBuilder asmBuilder = new(name, coreAssembly: typeof(object).Assembly, null);

            var modBuilder = asmBuilder.DefineDynamicModule(assemblyName);


            //Define every class and it's fields/methods first so that they can be referenced in method bodies
            foreach (var classy in scopes.classes)
            {
                MakeClass(classy.Value, modBuilder);
            }

            var main = classBuilders["Program"].Methods.First(x => x.Key.Name == "Main");
            var il = main.Value.GetILGenerator();
            MainLabels = MapLabels(il, main.Key.Labels);


            // Complete the types
            foreach (var classy in classBuilders)
            {
                classy.Value.TypeBuilder.CreateType();
            }

            EmitEverything(tree, scopes);

            il.Emit(OpCodes.Ret);

            //Generate metadata and assembly
            var metadata = asmBuilder.GenerateMetadata(out var ilStream, out var fieldData);

            //Build the PE file
            var peBuilder = new ManagedPEBuilder(
                new PEHeaderBuilder(subsystem: Subsystem.WindowsCui),
                new MetadataRootBuilder(metadata),
                ilStream,
                fieldData,
                entryPoint: MetadataTokens.MethodDefinitionHandle(classBuilders["Program"].Methods.First(x => x.Key.Name == "Main").Value.MetadataToken));

            var peBlob = new BlobBuilder();
            peBuilder.Serialize(peBlob);


            //Save to disk
            using var fileStream = new FileStream(fileName, FileMode.Create);
            peBlob.WriteContentTo(fileStream);


            return "";
        }

        private static void MakeClass(ClassInfo classy, ModuleBuilder modBuilder)
        {
            // Define type
            classBuilders.Add(classy.Name, new ClassBuilder(classy, modBuilder.DefineType(classy.Name, TypeAttributes.Public | TypeAttributes.Class)));

            TypeMap.Add(classy.Name, classBuilders[classy.Name].TypeBuilder);

            foreach (var field in classy.Fields)
            {
                classBuilders[classy.Name].TypeBuilder.DefineField(field.Key, TypeMap[field.Value.Type], FieldAttributes.Public);
            }
            foreach (var method in classy.Methods)
            {
                MethodAttributes attributes = MethodAttributes.Public;
                if (/*method.Name == "Main" &&*/ method.Owner.Name == "Program")
                {
                    attributes |= MethodAttributes.Static;
                }

                classBuilders[classy.Name].Methods.Add(method, classBuilders[classy.Name].TypeBuilder.DefineMethod(
                    method.Name,
                    attributes,
                    returnType: TypeMap[method.ReturnType],
                    parameterTypes: method.Parameters.Select(x => TypeMap[x.Type]).ToArray()));
            }
        }

        private static void EmitEverything(ParseNode tree, ScopeStack scopes)
        {
            //top level functions (in Program)
            if (tree is FunctionDeclaration funcy)
            {
                StartEmittingMethod("Program", funcy);
            }
            else if (tree is ClassDeclaration classy)
            {
                foreach (var child in classy.Children.Where(x => x is FunctionDeclaration))
                {
                    if (child is ConstructorDeclaration)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        StartEmittingMethod(classy.Name, (child as FunctionDeclaration)!);
                    }
                }
            }
            else if (tree is Program)//this should only be top level code
            {
                foreach (var child in tree.Children)
                {
                    EmitEverything((child as ParseNode)!, scopes);
                }
            }
            else //we're in main
            {
                var main = classBuilders["Program"].Methods.First(x => x.Key.Name == "Main");
                var il = main.Value.GetILGenerator();

                EmitMethodBody(il, tree, MainLocals, [], MainSymbols, MainLabels, classBuilders["Program"].Methods);
            }

        }
        private static void StartEmittingMethod(string className, FunctionDeclaration func)
        {
            var method = classBuilders[className].Methods.First(x => x.Key.Name == func.Name && x.Key.Parameters.SequenceEqual(ConvertParams(func.Parameters)));
            var il = method.Value.GetILGenerator();
            var labels = MapLabels(il, method.Key.Labels);

            Dictionary<string, int> args = [];
            Dictionary<string, Type> symbols = [];
            for (int i = 0; i < func.Parameters.Count; i++)
            {
                args.Add(func.Parameters[i].Name, i);
                symbols.Add(func.Parameters[i].Name, TypeMap[func.Parameters[i].Type]);
            }

            foreach (var child in func.Children)
            {
                EmitMethodBody(il, (child as ParseNode)!, [], args, symbols, labels, classBuilders[className].Methods);
            }
            il.Emit(OpCodes.Ret);
        }


        private static void EmitMethodBody(ILGenerator il,
                                           ParseNode node,
                                           Dictionary<string, int> locals,
                                           Dictionary<string, int> args,
                                           Dictionary<string, Type> symbols,
                                           Dictionary<string, System.Reflection.Emit.Label> labels,
                                           Dictionary<Function, MethodBuilder> localMethods)
        {
            if (node is ASTNode ast)
            {
                #region basic values
                if (ast.Token is NumericValue number)
                {
                    il.Emit(OpCodes.Ldc_I4, number.Number);
                }
                else if (ast.Token is StringValue str)
                {
                    il.Emit(OpCodes.Ldstr, str.Value);
                }
                else if (ast.Token is TrueKeyword)
                {
                    il.Emit(OpCodes.Ldc_I4_1);
                }
                else if (ast.Token is FalseKeyword)
                {
                    il.Emit(OpCodes.Ldc_I4_0);
                }
                #endregion
                else if (ast.Token is Identifier id)
                {
                    if (locals.TryGetValue(id.Text, out int localId))
                    {
                        il.Emit(OpCodes.Ldloc, localId);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldarg, args[id.Text]);
                    }
                }
                else if (MathOperators.TryGetValue(ast.Token.GetType(), out var MathOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, labels, localMethods);

                    il.Emit(MathOp);
                    return;
                }
                else if (Comparisons.TryGetValue(ast.Token.GetType(), out var CompOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, labels, localMethods);

                    il.Emit(CompOp);
                    return;
                }
                else if (BoolOperators.TryGetValue(ast.Token.GetType(), out var BoolOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, labels, localMethods);

                    il.Emit(BoolOp);
                    return;
                }
                else if (AnnoyingComparisons.TryGetValue(ast.Token.GetType(), out var ACompOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, labels, localMethods);

                    il.Emit(ACompOp);

                    //Negate result of comparison
                    il.Emit(OpCodes.Ldc_I4_0);//load false
                    il.Emit(OpCodes.Ceq);//essentially !bool => bool == false
                    return;
                }
                else if (ast.Token is NotOperator)
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);

                    il.Emit(OpCodes.Ldc_I4_0);//load false
                    il.Emit(OpCodes.Ceq);//essentially !bool => bool == false
                    return;
                }
                else if (ast.Token is ElseKeyword)
                {
                    //body
                    EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);
                    return;
                }
                else if (ast.Token is Tokens.Label label)
                {
                    il.MarkLabel(labels[label.Name]);
                }
            }
            else if (node is VariableDeclaration decl)
            {
                symbols.Add(decl.Name, il.DeclareLocal(TypeMap[decl.TypeExpected]).LocalType);
                locals.Add(decl.Name, locals.Count);

                EmitMethodBody(il, (decl.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);

                il.Emit(OpCodes.Stloc, locals[decl.Name]);
                return;
            }
            else if (node is VariableAssignment assignment)
            {
                if (assignment.Name!.Owner != "")
                {
                    il.Emit(OpCodes.Ldloc, locals[assignment.Name.Owner]);
                }

                EmitMethodBody(il, (assignment.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);

                if (assignment.Name.Owner == "")
                {
                    il.Emit(OpCodes.Stloc, locals[assignment.Name.Name]);
                }
                else
                {
                    il.Emit(OpCodes.Stfld, classBuilders[symbols[assignment.Name.Owner].Name].TypeBuilder.GetField(assignment.Name.Name)!);
                }
                return;
            }
            else if (node is Incrementer incrementer)
            {
                if (incrementer.Name!.Owner == "")
                {
                    if (incrementer.IsPre)
                    {
                        il.Emit(OpCodes.Ldloc, locals[incrementer.Name.Name]);//load current value
                        il.Emit(OpCodes.Ldc_I4_1);//load 1
                        if (incrementer.IsIncrement)
                        {
                            il.Emit(OpCodes.Add);
                        }
                        else
                        {
                            il.Emit(OpCodes.Sub);
                        }

                        if (node is ExpressionIncrementer)//to leave the value on the stack
                        {
                            il.Emit(OpCodes.Dup);
                        }
                        il.Emit(OpCodes.Stloc, locals[incrementer.Name.Name]);//store incremented value
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldloc, locals[incrementer.Name.Name]);//load current value

                        if (node is ExpressionIncrementer)//to leave the value on the stack
                        {
                            il.Emit(OpCodes.Dup);
                        }
                        il.Emit(OpCodes.Ldc_I4_1);//load 1
                        if (incrementer.IsIncrement)
                        {
                            il.Emit(OpCodes.Add);
                        }
                        else
                        {
                            il.Emit(OpCodes.Sub);
                        }
                        il.Emit(OpCodes.Stloc, locals[incrementer.Name.Name]);//store incremented value
                    }
                }
                else
                {
                    var field = classBuilders[incrementer.Name.Owner].TypeBuilder.GetField(incrementer.Name.Name)!;

                    // Two refs: one consumed by ldfld, one stays for stfld
                    il.Emit(OpCodes.Ldloc, locals[incrementer.Name.Owner]);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldfld, field); // stack: [obj, oldValue]

                    if (incrementer.IsPre)
                    {
                        il.Emit(OpCodes.Ldc_I4_1);
                        il.Emit(incrementer.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                        // stack: [obj, newValue]

                        if (node is ExpressionIncrementer)
                        {
                            var temp = il.DeclareLocal(typeof(int));
                            il.Emit(OpCodes.Dup);         // [obj, newValue, newValue]
                            il.Emit(OpCodes.Stloc, temp);  // [obj, newValue]
                            il.Emit(OpCodes.Stfld, field); // []
                            il.Emit(OpCodes.Ldloc, temp);  // [newValue] ← left on stack
                        }
                        else
                        {
                            il.Emit(OpCodes.Stfld, field); // []
                        }
                    }
                    else
                    {
                        // stack: [obj, oldValue]
                        if (node is ExpressionIncrementer)
                        {
                            var temp = il.DeclareLocal(typeof(int));
                            il.Emit(OpCodes.Dup);         // [obj, oldValue, oldValue]
                            il.Emit(OpCodes.Stloc, temp);  // [obj, oldValue]
                            il.Emit(OpCodes.Ldc_I4_1);
                            il.Emit(incrementer.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                            il.Emit(OpCodes.Stfld, field); // []
                            il.Emit(OpCodes.Ldloc, temp);  // [oldValue] ← left on stack
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldc_I4_1);
                            il.Emit(incrementer.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                            il.Emit(OpCodes.Stfld, field); // []
                        }
                    }
                }
            }
            else if (node is IfStatement @if)
            {
                //condition
                EmitMethodBody(il, @if.Condition!, locals, args, symbols, labels, localMethods);


                var ifFalseLabel = il.DefineLabel();

                il.Emit(OpCodes.Brfalse, ifFalseLabel);//skip to followup if false

                if (@if.Body != null)
                {
                    //body
                    EmitMethodBody(il, @if.Body, locals, args, symbols, labels, localMethods);
                }

                var ifTrueLabel = il.DefineLabel();

                il.Emit(OpCodes.Br, ifTrueLabel);//skip followup if true

                il.MarkLabel(ifFalseLabel);

                if (@if.Followup != null)//there is a followup
                {
                    //followup
                    EmitMethodBody(il, @if.Followup, locals, args, symbols, labels, localMethods);
                }

                il.MarkLabel(ifTrueLabel);
                return;
            }
            else if (node is IfntStatement ifnt)
            {
                //condition
                EmitMethodBody(il, ifnt.Condition!, locals, args, symbols, labels, localMethods);

                var ifTrueLabel = il.DefineLabel();

                il.Emit(OpCodes.Brtrue, ifTrueLabel);//skip to followup if true

                if (ifnt.Body != null)
                {
                    //body
                    EmitMethodBody(il, ifnt.Body, locals, args, symbols, labels, localMethods);

                }

                var ifFalseLabel = il.DefineLabel();

                il.Emit(OpCodes.Br, ifFalseLabel);//skip followup if false

                il.MarkLabel(ifTrueLabel);

                if (ifnt.Followup != null)//there is a followup
                {
                    //followup
                    EmitMethodBody(il, ifnt.Followup, locals, args, symbols, labels, localMethods);
                }

                il.MarkLabel(ifFalseLabel);
                return;
            }
            else if (node is WhileLoop @while)
            {
                var LoopLabel = il.DefineLabel();
                var ifFalseLabel = il.DefineLabel();
                var followUpLabel = il.DefineLabel();


                if (@while.Followup != null)
                {
                    //condition
                    EmitMethodBody(il, @while.Condition!, locals, args, symbols, labels, localMethods);

                    il.Emit(OpCodes.Brfalse, followUpLabel);
                }

                il.MarkLabel(LoopLabel);

                //condition
                EmitMethodBody(il, @while.Condition!, locals, args, symbols, labels, localMethods);

                il.Emit(OpCodes.Brfalse, ifFalseLabel);

                if (@while.Body != null)//there is a body
                {
                    //body
                    EmitMethodBody(il, @while.Body!, locals, args, symbols, labels, localMethods);
                }

                il.Emit(OpCodes.Br, LoopLabel);


                if (@while.Followup != null)//there is a followup
                {
                    il.MarkLabel(followUpLabel);

                    //followup
                    EmitMethodBody(il, @while.Followup!, locals, args, symbols, labels, localMethods);
                }

                il.MarkLabel(ifFalseLabel);
                return;
            }
            else if (node is ForLoop)
            {
                EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, args, symbols, labels, localMethods);//variable init

                var ConditionLabel = il.DefineLabel();
                il.Emit(OpCodes.Br_S, ConditionLabel);//skip to condition first


                var LoopLabel = il.DefineLabel();
                il.MarkLabel(LoopLabel);

                if (node.Children.Count > 3)//if there is a body
                {
                    EmitMethodBody(il, (node.Children[3] as ParseNode)!, locals, args, symbols, labels, localMethods);//body
                }

                EmitMethodBody(il, (node.Children[2] as ParseNode)!, locals, args, symbols, labels, localMethods);//increment



                il.MarkLabel(ConditionLabel);

                EmitMethodBody(il, (node.Children[1] as ParseNode)!, locals, args, symbols, labels, localMethods);//condition
                il.Emit(OpCodes.Brtrue, LoopLabel);//loop if condition true


                return;
            }
            else if (node is GotoStatement @goto)
            {
                il.Emit(OpCodes.Br_S, labels[@goto.LabelName]);
            }
            else if (node is FunctionCall call)
            {
                for (int i = 0; i < call.Parameters.Count; i++)
                {
                    EmitMethodBody(il, call.Parameters[i], locals, args, symbols, labels, localMethods);
                }
                if (call.Owner == "")
                {
                    il.EmitCall(OpCodes.Call, localMethods[call.Target!], null);
                }
                else
                {
                    il.EmitCall(OpCodes.Call, classBuilders[call.Owner].Methods[call.Target!], null);
                }
                return;
            }
            else if (node is ReturnStatement @return)
            {
                if (@return.Value is not null)
                {
                    EmitMethodBody(il, @return.Value, locals, args, symbols, labels, localMethods);
                }

                il.Emit(OpCodes.Ret);
                return;
            }
            else if (node is PrintStatement)
            {
                var body = (node.Children[0] as ParseNode)!;
                EmitMethodBody(il, body, locals, args, symbols, labels, localMethods);
                if (body is ASTNode asty)
                {
                    if (asty.Token is StringValue)
                    {
                        il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [typeof(string)])!);
                    }
                    else if (asty.Token is Identifier)
                    {
                        string variableName = asty.Token.Text;
                        il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [symbols[variableName]])!);
                    }
                }
                else
                {
                    if (body is VariableName name)
                    {
                        if (name.Owner == "")
                        {
                            il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [symbols[name.Name]])!);
                        }
                        else
                        { 
                            il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [TypeMap[classBuilders[symbols[name.Owner].Name].ClassInfo.Fields[name.Name].Type]])!);
                        }
                    }
                    else
                    {
                        ;
                    }
                }
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is ParseNode pn)
                {
                    EmitMethodBody(il, pn, locals, args, symbols, labels, localMethods);
                }
            }
        }

        private static Dictionary<string, System.Reflection.Emit.Label> MapLabels(ILGenerator il, List<string> labels)
        {
            Dictionary<string, System.Reflection.Emit.Label> Labels = [];
            foreach (var label in labels)
            {
                Labels.Add(label, il.DefineLabel());
            }
            return Labels;
        }


        private static List<VarInfo> ConvertParams(List<FunctionParameter> parameters) => parameters.Select(x => new VarInfo(x.Type)).ToList();
    }
}