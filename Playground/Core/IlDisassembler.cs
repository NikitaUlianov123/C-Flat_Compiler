using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Playground.Core;

/// <summary>
/// A small ildasm-style disassembler for the assemblies the C-Flat compiler emits.
/// It reads the PE image with System.Reflection.Metadata, so it never loads or runs the code.
/// </summary>
public static class IlDisassembler
{
    private static readonly OpCode[] OneByte = new OpCode[0x100];
    private static readonly OpCode[] TwoByte = new OpCode[0x100];

    static IlDisassembler()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode op) continue;
            ushort value = unchecked((ushort)op.Value);
            if (op.Size == 1) OneByte[value] = op;
            else TwoByte[value & 0xFF] = op;
        }
    }

    public static string Disassemble(byte[] image)
    {
        using var pe = new PEReader(ImmutableArray.Create(image));
        var md = pe.GetMetadataReader();
        var names = new SignatureNames(md);
        var sb = new StringBuilder();

        foreach (var handle in md.AssemblyReferences)
        {
            sb.AppendLine($".assembly extern {md.GetString(md.GetAssemblyReference(handle).Name)} {{ }}");
        }
        if (md.IsAssembly)
        {
            sb.AppendLine($".assembly {md.GetString(md.GetAssemblyDefinition().Name)} {{ }}");
        }

        int entryPoint = pe.PEHeaders.CorHeader?.EntryPointTokenOrRelativeVirtualAddress ?? 0;

        foreach (var typeHandle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(typeHandle);
            string typeName = md.GetString(type.Name);
            if (typeName == "<Module>" && type.GetMethods().Count == 0 && type.GetFields().Count == 0) continue;

            sb.AppendLine();
            sb.AppendLine($".class {TypeAttributesText(type.Attributes)}{names.TypeDefName(typeHandle)}");
            sb.AppendLine($"       extends {BaseTypeName(md, names, type)}");
            sb.AppendLine("{");

            foreach (var fieldHandle in type.GetFields())
            {
                var field = md.GetFieldDefinition(fieldHandle);
                string fieldType = field.DecodeSignature(names, null);
                sb.AppendLine($"  .field {FieldAttributesText(field.Attributes)}{fieldType} {md.GetString(field.Name)}");
            }

            foreach (var methodHandle in type.GetMethods())
            {
                sb.AppendLine();
                WriteMethod(sb, pe, md, names, methodHandle, MetadataTokens.GetToken(methodHandle) == entryPoint);
            }

            sb.AppendLine("}");
        }

        return sb.ToString().TrimStart();
    }

    private static string BaseTypeName(MetadataReader md, SignatureNames names, TypeDefinition type) =>
        type.BaseType.IsNil ? "[none]" : names.TypeName(type.BaseType);

    private static void WriteMethod(StringBuilder sb, PEReader pe, MetadataReader md, SignatureNames names,
                                    MethodDefinitionHandle handle, bool isEntryPoint)
    {
        var method = md.GetMethodDefinition(handle);
        var sig = method.DecodeSignature(names, null);
        string name = md.GetString(method.Name);
        string instance = sig.Header.IsInstance ? "instance " : "";

        sb.AppendLine($"  .method {MethodAttributesText(method.Attributes)}{instance}{sig.ReturnType} {name}({string.Join(", ", sig.ParameterTypes)})");
        sb.AppendLine("  {");
        if (isEntryPoint) sb.AppendLine("    .entrypoint");

        if (method.RelativeVirtualAddress == 0)
        {
            sb.AppendLine("    // no method body was emitted");
            sb.AppendLine("  }");
            return;
        }

        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        sb.AppendLine($"    .maxstack {body.MaxStack}");

        if (!body.LocalSignature.IsNil)
        {
            var locals = md.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(names, null);
            if (locals.Length > 0)
            {
                sb.AppendLine($"    .locals {(body.LocalVariablesInitialized ? "init " : "")}(");
                for (int i = 0; i < locals.Length; i++)
                {
                    sb.AppendLine($"      [{i}] {locals[i]} V_{i}{(i < locals.Length - 1 ? "," : "")}");
                }
                sb.AppendLine("    )");
            }
        }

        sb.AppendLine();
        WriteInstructions(sb, md, names, body.GetILReader());
        sb.AppendLine("  }");
    }

    private static void WriteInstructions(StringBuilder sb, MetadataReader md, SignatureNames names, BlobReader il)
    {
        while (il.RemainingBytes > 0)
        {
            int offset = il.Offset;
            byte first = il.ReadByte();
            OpCode op = first == 0xFE && il.RemainingBytes > 0 ? TwoByte[il.ReadByte()] : OneByte[first];

            sb.Append($"    IL_{offset:x4}: ");
            if (op.Size == 0)
            {
                sb.AppendLine($".byte 0x{first:x2}");
                continue;
            }

            sb.Append(op.Name);
            string? operand = ReadOperand(ref il, op, md, names);
            if (operand is not null)
            {
                sb.Append(' ', Math.Max(1, 12 - op.Name!.Length));
                sb.Append(operand);
            }
            sb.AppendLine();
        }
    }

    private static string? ReadOperand(ref BlobReader il, OpCode op, MetadataReader md, SignatureNames names)
    {
        switch (op.OperandType)
        {
            case OperandType.InlineNone:
                return null;
            case OperandType.ShortInlineBrTarget:
            {
                int delta = il.ReadSByte();
                return $"IL_{il.Offset + delta:x4}";
            }
            case OperandType.InlineBrTarget:
            {
                int delta = il.ReadInt32();
                return $"IL_{il.Offset + delta:x4}";
            }
            case OperandType.ShortInlineI:
                return op == OpCodes.Ldc_I4_S ? il.ReadSByte().ToString() : il.ReadByte().ToString();
            case OperandType.InlineI:
                return il.ReadInt32().ToString();
            case OperandType.InlineI8:
                return il.ReadInt64().ToString();
            case OperandType.ShortInlineR:
                return il.ReadSingle().ToString("R");
            case OperandType.InlineR:
                return il.ReadDouble().ToString("R");
            case OperandType.ShortInlineVar:
                return VarName(op, il.ReadByte());
            case OperandType.InlineVar:
                return VarName(op, il.ReadUInt16());
            case OperandType.InlineString:
                return Quote(md.GetUserString(MetadataTokens.UserStringHandle(il.ReadInt32() & 0xFFFFFF)));
            case OperandType.InlineSwitch:
            {
                uint count = il.ReadUInt32();
                var deltas = new int[count];
                for (int i = 0; i < count; i++) deltas[i] = il.ReadInt32();
                int origin = il.Offset;
                return "(" + string.Join(", ", deltas.Select(d => $"IL_{origin + d:x4}")) + ")";
            }
            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
            case OperandType.InlineSig:
                return names.TokenName(il.ReadInt32());
            default:
                return null;
        }
    }

    private static string VarName(OpCode op, int index) =>
        op.Name!.Contains("loc") ? $"V_{index}" : index.ToString();

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            sb.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }
        return sb.Append('"').ToString();
    }

    private static string TypeAttributesText(TypeAttributes a)
    {
        var parts = new List<string>();
        if ((a & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface) parts.Add("interface");
        parts.Add((a & TypeAttributes.VisibilityMask) switch
        {
            TypeAttributes.Public => "public",
            TypeAttributes.NotPublic => "private",
            _ => "nested",
        });
        if ((a & TypeAttributes.Abstract) != 0) parts.Add("abstract");
        if ((a & TypeAttributes.Sealed) != 0) parts.Add("sealed");
        if ((a & TypeAttributes.BeforeFieldInit) != 0) parts.Add("beforefieldinit");
        return string.Join(" ", parts) + " ";
    }

    private static string FieldAttributesText(FieldAttributes a)
    {
        var parts = new List<string>
        {
            (a & FieldAttributes.FieldAccessMask) switch
            {
                FieldAttributes.Public => "public",
                FieldAttributes.Private => "private",
                FieldAttributes.Family => "family",
                FieldAttributes.Assembly => "assembly",
                _ => "privatescope",
            },
        };
        if ((a & FieldAttributes.Static) != 0) parts.Add("static");
        if ((a & FieldAttributes.InitOnly) != 0) parts.Add("initonly");
        return string.Join(" ", parts) + " ";
    }

    private static string MethodAttributesText(MethodAttributes a)
    {
        var parts = new List<string>
        {
            (a & MethodAttributes.MemberAccessMask) switch
            {
                MethodAttributes.Public => "public",
                MethodAttributes.Private => "private",
                MethodAttributes.Family => "family",
                MethodAttributes.Assembly => "assembly",
                _ => "privatescope",
            },
        };
        if ((a & MethodAttributes.HideBySig) != 0) parts.Add("hidebysig");
        if ((a & MethodAttributes.SpecialName) != 0) parts.Add("specialname");
        if ((a & MethodAttributes.RTSpecialName) != 0) parts.Add("rtspecialname");
        if ((a & MethodAttributes.Static) != 0) parts.Add("static");
        if ((a & MethodAttributes.Virtual) != 0) parts.Add("virtual");
        return string.Join(" ", parts) + " ";
    }

    /// <summary>Formats types, methods and fields the way ildasm writes them.</summary>
    private sealed class SignatureNames(MetadataReader md) : ISignatureTypeProvider<string, object?>
    {
        public string TokenName(int token)
        {
            var handle = MetadataTokens.EntityHandle(token);
            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    var method = md.GetMethodDefinition((MethodDefinitionHandle)handle);
                    var sig = method.DecodeSignature(this, null);
                    return FormatMethod(sig, TypeDefName(method.GetDeclaringType()), md.GetString(method.Name));
                }
                case HandleKind.FieldDefinition:
                {
                    var field = md.GetFieldDefinition((FieldDefinitionHandle)handle);
                    return $"{field.DecodeSignature(this, null)} {TypeDefName(field.GetDeclaringType())}::{md.GetString(field.Name)}";
                }
                case HandleKind.MemberReference:
                {
                    var member = md.GetMemberReference((MemberReferenceHandle)handle);
                    string parent = member.Parent.IsNil ? "" : TypeName(member.Parent);
                    string name = md.GetString(member.Name);
                    if (member.GetKind() == MemberReferenceKind.Field)
                    {
                        return $"{member.DecodeFieldSignature(this, null)} {parent}::{name}";
                    }
                    return FormatMethod(member.DecodeMethodSignature(this, null), parent, name);
                }
                case HandleKind.TypeDefinition:
                case HandleKind.TypeReference:
                case HandleKind.TypeSpecification:
                    return TypeName(handle);
                default:
                    return $"0x{token:x8}";
            }
        }

        private static string FormatMethod(MethodSignature<string> sig, string owner, string name)
        {
            string instance = sig.Header.IsInstance ? "instance " : "";
            return $"{instance}{sig.ReturnType} {owner}::{name}({string.Join(", ", sig.ParameterTypes)})";
        }

        public string TypeName(EntityHandle handle) => handle.Kind switch
        {
            HandleKind.TypeDefinition => TypeDefName((TypeDefinitionHandle)handle),
            HandleKind.TypeReference => TypeRefName((TypeReferenceHandle)handle),
            HandleKind.TypeSpecification => md.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null),
            HandleKind.MethodDefinition => md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name),
            _ => handle.Kind.ToString(),
        };

        public string TypeDefName(TypeDefinitionHandle handle)
        {
            var type = md.GetTypeDefinition(handle);
            string ns = md.GetString(type.Namespace);
            string name = md.GetString(type.Name);
            return ns.Length == 0 ? name : $"{ns}.{name}";
        }

        private string TypeRefName(TypeReferenceHandle handle)
        {
            var type = md.GetTypeReference(handle);
            string ns = md.GetString(type.Namespace);
            string name = md.GetString(type.Name);
            string full = ns.Length == 0 ? name : $"{ns}.{name}";
            return type.ResolutionScope.Kind == HandleKind.AssemblyReference
                ? $"[{md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name)}]{full}"
                : full;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.SByte => "int8",
            PrimitiveTypeCode.Byte => "uint8",
            PrimitiveTypeCode.Int16 => "int16",
            PrimitiveTypeCode.UInt16 => "uint16",
            PrimitiveTypeCode.Int32 => "int32",
            PrimitiveTypeCode.UInt32 => "uint32",
            PrimitiveTypeCode.Int64 => "int64",
            PrimitiveTypeCode.UInt64 => "uint64",
            PrimitiveTypeCode.Single => "float32",
            PrimitiveTypeCode.Double => "float64",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.IntPtr => "native int",
            PrimitiveTypeCode.UIntPtr => "native uint",
            PrimitiveTypeCode.TypedReference => "typedref",
            _ => typeCode.ToString(),
        };

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            Prefix(rawTypeKind) + TypeDefName(handle);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            Prefix(rawTypeKind) + TypeRefName(handle);

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        private static string Prefix(byte rawTypeKind) =>
            rawTypeKind == (byte)SignatureTypeKind.ValueType ? "valuetype " : "class ";

        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPinnedType(string elementType) => elementType + " pinned";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            $"{unmodifiedType} {(isRequired ? "modreq" : "modopt")}({modifier})";
        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"method {signature.ReturnType} *({string.Join(", ", signature.ParameterTypes)})";
    }
}
