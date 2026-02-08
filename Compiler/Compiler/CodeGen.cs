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

        private static readonly Dictionary<string, Type> TypeMap = new()
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

        private static Dictionary<string, System.Reflection.Emit.Label> Labels = [];


        public static string GenerateCode(ParseNode tree,
                                          List<string> labels,
                                          string assemblyName = "EmittedProgram")
        {
            string fileName = assemblyName + ".exe";

            // Define the assembly and module
            var name = new AssemblyName(assemblyName);
            PersistedAssemblyBuilder asmBuilder = new(name, coreAssembly: typeof(object).Assembly, null);

            var modBuilder = asmBuilder.DefineDynamicModule(assemblyName);

            // Define type "Program"
            var typeBuilder = modBuilder.DefineType("Program", TypeAttributes.Public | TypeAttributes.Class);

            // Define static Main method
            var methodBuilder = typeBuilder.DefineMethod("Main",
                MethodAttributes.Public | MethodAttributes.Static,
                returnType: typeof(void),
                parameterTypes: Type.EmptyTypes);

            // Mark as entry point
            //methodBuilder

            var il = methodBuilder.GetILGenerator();


            //Make functions:
            Dictionary<string, MethodInfo> methods = [];

            foreach (var node in (tree.Children[0] as ParseNode)!.Children)
            {
                if (node is FunctionDeclaration func)
                {
                    var tempMethodBuilder = typeBuilder.DefineMethod(func.Name,
                        MethodAttributes.Public | MethodAttributes.Static,
                        returnType: TypeMap[func.ReturnType],
                        parameterTypes: func.Parameters.Select(x => TypeMap[x.Type]).ToArray());


                    ILGenerator methodIL = tempMethodBuilder.GetILGenerator();


                    Dictionary<string, int> args = [];
                    Dictionary<string, Type> symbols = [];
                    for (int i = 0; i < func.Parameters.Count; i++)
                    {
                        args.Add(func.Parameters[i].Name, i);
                        symbols.Add(func.Parameters[i].Name, TypeMap[func.Parameters[i].Type]);
                    }

                    EmitMethodBody(methodIL, (func.Children[0] as ParseNode)!, [], args, symbols, methods);

                    methodIL.Emit(OpCodes.Ret);


                    methods.Add(func.Name, tempMethodBuilder);
                }
            }

            //Map labels
            MapLabels(il, labels);

            //code go here
            EmitMethodBody(il, tree, [], [], [], methods);


            il.Emit(OpCodes.Ret);

            // Complete the type
            typeBuilder.CreateType();

            //Generate metadata and assembly
            var metadata = asmBuilder.GenerateMetadata(out var ilStream, out var fieldData);

            //Build the PE file
            var peBuilder = new ManagedPEBuilder(
                new PEHeaderBuilder(subsystem: Subsystem.WindowsCui),
                new MetadataRootBuilder(metadata),
                ilStream,
                fieldData,
                entryPoint: MetadataTokens.MethodDefinitionHandle(methodBuilder.MetadataToken));

            var peBlob = new BlobBuilder();
            peBuilder.Serialize(peBlob);


            //Save to disk
            using var fileStream = new FileStream(fileName, FileMode.Create);
            peBlob.WriteContentTo(fileStream);


            return "";
        }

        private static void EmitMethodBody(ILGenerator il,
                                           ParseNode node,
                                           Dictionary<string, int> locals,
                                           Dictionary<string, int> args,
                                           Dictionary<string, Type> symbols,
                                           Dictionary<string, MethodInfo> methods)
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
                else if (ast.Token is PrintKeyword)
                {
                    var body = (ast.Children[0] as ASTNode)!;
                    EmitMethodBody(il, body, locals, args, symbols, methods);
                    if (body.Token is StringValue)
                    {
                        il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [typeof(string)])!);
                    }
                    else if (body.Token is Identifier)
                    {
                        string variableName = body.Token.Text;
                        il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [symbols[variableName]])!);
                    }
                    return;
                }
                else if (MathOperators.TryGetValue(ast.Token.GetType(), out var MathOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, methods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, methods);

                    il.Emit(MathOp);
                    return;
                }
                else if (Comparisons.TryGetValue(ast.Token.GetType(), out var CompOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, methods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, methods);

                    il.Emit(CompOp);
                    return;
                }
                else if (BoolOperators.TryGetValue(ast.Token.GetType(), out var BoolOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, methods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, methods);

                    il.Emit(BoolOp);
                    return;
                }
                else if (AnnoyingComparisons.TryGetValue(ast.Token.GetType(), out var ACompOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, methods);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, args, symbols, methods);

                    il.Emit(ACompOp);

                    //Negate result of comparison
                    il.Emit(OpCodes.Ldc_I4_0);//load false
                    il.Emit(OpCodes.Ceq);//essentially !bool => bool == false
                    return;
                }
                else if (ast.Token is NotOperator)
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, args, symbols, methods);

                    il.Emit(OpCodes.Ldc_I4_0);//load false
                    il.Emit(OpCodes.Ceq);//essentially !bool => bool == false
                    return;
                }
                else if (ast.Token is ElseKeyword)
                {
                    //body
                    EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, args, symbols, methods);
                    return;
                }
                else if (ast.Token is Tokens.Label label)
                {
                    il.MarkLabel(Labels[label.Name]);
                }
            }
            else if (node is VariableDeclaration decl)
            {
                symbols.Add(decl.Name, il.DeclareLocal(TypeMap[decl.Type]).LocalType);
                locals.Add(decl.Name, locals.Count);

                EmitMethodBody(il, (decl.Children[0] as ParseNode)!, locals, args, symbols, methods);

                il.Emit(OpCodes.Stloc, locals[decl.Name]);
                return;
            }
            else if (node is VariableAssignment assignment)
            {
                EmitMethodBody(il, (assignment.Children[0] as ParseNode)!, locals, args, symbols, methods);

                il.Emit(OpCodes.Stloc, locals[assignment.Name]);
                return;
            }
            else if (node is Incrementer incrementer)
            {
                if (incrementer.IsPre)
                {
                    il.Emit(OpCodes.Ldloc, locals[incrementer.Name]);//load current value
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
                    il.Emit(OpCodes.Stloc, locals[incrementer.Name]);//store incremented value
                }
                else
                {
                    il.Emit(OpCodes.Ldloc, locals[incrementer.Name]);//load current value

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
                    il.Emit(OpCodes.Stloc, locals[incrementer.Name]);//store incremented value
                }
            }
            else if (node is IfStatement @if)
            {
                //condition
                EmitMethodBody(il, @if.Condition!, locals, args, symbols, methods);


                var ifFalseLabel = il.DefineLabel();

                il.Emit(OpCodes.Brfalse, ifFalseLabel);//skip to followup if false

                if (@if.Body != null)
                {
                    //body
                    EmitMethodBody(il, @if.Body, locals, args, symbols, methods);
                }

                var ifTrueLabel = il.DefineLabel();

                il.Emit(OpCodes.Br, ifTrueLabel);//skip followup if true

                il.MarkLabel(ifFalseLabel);

                if (@if.Followup != null)//there is a followup
                {
                    //followup
                    EmitMethodBody(il, @if.Followup, locals, args, symbols, methods);
                }

                il.MarkLabel(ifTrueLabel);
                return;
            }
            else if (node is IfntStatement ifnt)
            {
                //condition
                EmitMethodBody(il, ifnt.Condition!, locals, args, symbols, methods);

                var ifTrueLabel = il.DefineLabel();

                il.Emit(OpCodes.Brtrue, ifTrueLabel);//skip to followup if true

                if (ifnt.Body != null)
                {
                    //body
                    EmitMethodBody(il, ifnt.Body, locals, args, symbols, methods);

                }

                var ifFalseLabel = il.DefineLabel();

                il.Emit(OpCodes.Br, ifFalseLabel);//skip followup if false

                il.MarkLabel(ifTrueLabel);

                if (ifnt.Followup != null)//there is a followup
                {
                    //followup
                    EmitMethodBody(il, ifnt.Followup, locals, args, symbols, methods);
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
                    EmitMethodBody(il, @while.Condition!, locals, args, symbols, methods);

                    il.Emit(OpCodes.Brfalse, followUpLabel);
                }

                il.MarkLabel(LoopLabel);

                //condition
                EmitMethodBody(il, @while.Condition!, locals, args, symbols, methods);

                il.Emit(OpCodes.Brfalse, ifFalseLabel);

                if (@while.Body != null)//there is a body
                {
                    //body
                    EmitMethodBody(il, @while.Body!, locals, args, symbols, methods);
                }

                il.Emit(OpCodes.Br, LoopLabel);


                if (@while.Followup != null)//there is a followup
                {
                    il.MarkLabel(followUpLabel);

                    //followup
                    EmitMethodBody(il, @while.Followup!, locals, args, symbols, methods);
                }

                il.MarkLabel(ifFalseLabel);
                return;
            }
            else if (node is ForLoop)
            {
                EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, args, symbols, methods);//variable init

                var ConditionLabel = il.DefineLabel();
                il.Emit(OpCodes.Br_S, ConditionLabel);//skip to condition first


                var LoopLabel = il.DefineLabel();
                il.MarkLabel(LoopLabel);

                if (node.Children.Count > 3)//if there is a body
                {
                    EmitMethodBody(il, (node.Children[3] as ParseNode)!, locals, args, symbols, methods);//body
                }

                EmitMethodBody(il, (node.Children[2] as ParseNode)!, locals, args, symbols, methods);//increment



                il.MarkLabel(ConditionLabel);

                EmitMethodBody(il, (node.Children[1] as ParseNode)!, locals, args, symbols, methods);//condition
                il.Emit(OpCodes.Brtrue, LoopLabel);//loop if condition true


                return;
            }
            else if (node is GotoStatement @goto)
            {
                il.Emit(OpCodes.Br_S, Labels[@goto.LabelName]);
            }
            else if (node is FunctionDeclaration)
            {
                return;//should have already been emitted
            }
            else if (node is FunctionCall call)
            {
                for (int i = 0; i < call.Parameters.Count; i++)
                {
                    EmitMethodBody(il, call.Parameters[i], locals, args, symbols, methods);
                }

                il.EmitCall(OpCodes.Call, methods[call.Name], null);
                return;
            }
            else if (node is ReturnStatement @return)
            {
                if (@return.Value is not null)
                {
                    EmitMethodBody(il, @return.Value, locals, args, symbols, methods);
                }

                il.Emit(OpCodes.Ret);
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is ParseNode pn)
                {
                    EmitMethodBody(il, pn, locals, args, symbols, methods);
                }
            }
        }

        private static void MapLabels(ILGenerator il, List<string> labels)
        {
            foreach (var label in labels)
            {
                Labels.Add(label, il.DefineLabel());
            }
        }
    }
}