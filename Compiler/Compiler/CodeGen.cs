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
        };

        private static Dictionary<string, System.Reflection.Emit.Label> Labels = [];


        public static string GenerateCode(ParseNode tree, Dictionary<string, VarInfo> symbols, List<string> labels)
        {
            string assemblyName = "EmittedProgram";
            string fileName = assemblyName + ".exe";

            // Define the assembly and module
            var name = new AssemblyName(assemblyName);
            PersistedAssemblyBuilder asmBuilder = new PersistedAssemblyBuilder(name, coreAssembly: typeof(object).Assembly, null);

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


            //Map labels
            MapLabels(il, labels);

            //code go here
            EmitMethodBody(il, tree, [], symbols);


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

        private static void EmitMethodBody(ILGenerator il, ParseNode node, Dictionary<string, int> locals, Dictionary<string, VarInfo> symbols)
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
                    il.Emit(OpCodes.Ldstr, str.Text);
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
                    il.Emit(OpCodes.Ldloc, locals[id.Text]);
                }
                else if (ast.Token is PrintKeyword)
                {
                    if (ast.Children[0] is ASTNode astNode)
                    {
                        if (astNode.Token is StringValue)
                        {
                            il.Emit(OpCodes.Ldstr, astNode.Token.Text);
                            il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [typeof(string)])!);
                        }
                        else if (astNode.Token is Identifier)
                        {
                            string variableName = (ast.Children[0] as ASTNode)!.Token.Text;
                            il.Emit(OpCodes.Ldloc, locals[variableName]);
                            il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", [TypeMap[symbols[variableName].Type]])!);
                        }
                    }
                    return;
                }
                else if (MathOperators.TryGetValue(ast.Token.GetType(), out var MathOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, symbols);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, symbols);

                    il.Emit(MathOp);
                    return;
                }
                else if (Comparisons.TryGetValue(ast.Token.GetType(), out var CompOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, symbols);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, symbols);

                    il.Emit(CompOp);
                    return;
                }
                else if (BoolOperators.TryGetValue(ast.Token.GetType(), out var BoolOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, symbols);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, symbols);

                    il.Emit(BoolOp);
                    return;
                }
                else if (AnnoyingComparisons.TryGetValue(ast.Token.GetType(), out var ACompOp))
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, symbols);
                    EmitMethodBody(il, (ast.Children[1] as ParseNode)!, locals, symbols);

                    il.Emit(ACompOp);

                    //Negate result of comparison
                    il.Emit(OpCodes.Ldc_I4_0);//load false
                    il.Emit(OpCodes.Ceq);//essentially !bool => bool == false
                    return;
                }
                else if (ast.Token is NotOperator)
                {
                    EmitMethodBody(il, (ast.Children[0] as ParseNode)!, locals, symbols);

                    il.Emit(OpCodes.Ldc_I4_0);//load false
                    il.Emit(OpCodes.Ceq);//essentially !bool => bool == false
                    return;
                }
                else if (ast.Token is ElseKeyword)
                {
                    //body
                    EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, symbols);
                    return;
                }
                else if (ast.Token is Tokens.Label label)
                {
                    il.MarkLabel(Labels[label.Name]);
                }
            }
            else if (node is VariableDeclaration decl)
            {
                il.DeclareLocal(TypeMap[decl.Type]);
                locals.Add(decl.Name, locals.Count);

                EmitMethodBody(il, (decl.Children[0] as ParseNode)!, locals, symbols);

                il.Emit(OpCodes.Stloc, locals[decl.Name]);
                return;
            }
            else if (node is VariableAssignment assignment)
            {
                EmitMethodBody(il, (assignment.Children[0] as ParseNode)!, locals, symbols);

                il.Emit(OpCodes.Stloc, locals[assignment.Name]);
                return;
            }
            else if (node is IfStatement)
            {
                //condition
                EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, symbols);

                var ifFalseLabel = il.DefineLabel();

                il.Emit(OpCodes.Brfalse, ifFalseLabel);//skip to followup if false

                //body
                EmitMethodBody(il, (node.Children[1] as ParseNode)!, locals, symbols);

                var ifTrueLabel = il.DefineLabel();

                il.Emit(OpCodes.Br, ifTrueLabel);//skip followup if true

                il.MarkLabel(ifFalseLabel);

                if (node.Children.Count > 2)//there is a followup
                {
                    //followup
                    EmitMethodBody(il, (node.Children[2] as ParseNode)!, locals, symbols);
                }

                il.MarkLabel(ifTrueLabel);
                return;
            }
            else if (node is IfntStatement)
            {
                //condition
                EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, symbols);

                var ifTrueLabel = il.DefineLabel();

                il.Emit(OpCodes.Brtrue, ifTrueLabel);//skip to followup if true

                //body
                EmitMethodBody(il, (node.Children[1] as ParseNode)!, locals, symbols);

                var ifFalseLabel = il.DefineLabel();

                il.Emit(OpCodes.Br, ifFalseLabel);//skip followup if false

                il.MarkLabel(ifTrueLabel);

                if (node.Children.Count > 2)//there is a followup
                {
                    //followup
                    EmitMethodBody(il, (node.Children[2] as ParseNode)!, locals, symbols);
                }

                il.MarkLabel(ifFalseLabel);
                return;
            }
            else if (node is WhileLoop)
            {
                var LoopLabel = il.DefineLabel();
                var ifFalseLabel = il.DefineLabel();

                il.MarkLabel(LoopLabel);

                //condition
                EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, symbols);

                il.Emit(OpCodes.Brfalse, ifFalseLabel);

                if (node.Children.Count > 2)//there is a followup
                {
                    //body
                    EmitMethodBody(il, (node.Children[1] as ParseNode)!, locals, symbols);
                }

                il.Emit(OpCodes.Br, LoopLabel);

                il.MarkLabel(ifFalseLabel);
                return;
            }
            else if (node is ForLoop)
            {
                EmitMethodBody(il, (node.Children[0] as ParseNode)!, locals, symbols);//variable init

                var ConditionLabel = il.DefineLabel();
                il.Emit(OpCodes.Br_S, ConditionLabel);//skip to condition first


                var LoopLabel = il.DefineLabel();
                il.MarkLabel(LoopLabel);


                EmitMethodBody(il, (node.Children[3] as ParseNode)!, locals, symbols);//body


                EmitMethodBody(il, (node.Children[2] as ParseNode)!, locals, symbols);//increment



                il.MarkLabel(ConditionLabel);

                EmitMethodBody(il, (node.Children[1] as ParseNode)!, locals, symbols);//condition
                il.Emit(OpCodes.Brtrue, LoopLabel);//loop if condition true


                return;
            }
            else if (node is GotoStatement)
            {
                il.Emit(OpCodes.Br_S, Labels[(node.Children[0] as ASTNode)!.Token.Text]);
                return;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is ParseNode pn)
                {
                    EmitMethodBody(il, pn, locals, symbols);
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