// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tools

open Farkle.Grammars
open System
open System.Collections.Immutable
open System.Reflection
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.Reflection.PortableExecutable
open System.Text

type PrecompiledGrammar = {
    PEFile: PEReader
    MetadataReader: MetadataReader
    RVA: int
    Size: int
    Field: FieldDefinitionHandle
    DeclaringType: TypeDefinitionHandle
    Key: string | null
}
with
    member x.LoadGrammar() : Grammar =
        x.PEFile.GetSectionData(x.RVA).GetContent(0, x.Size)
        |> Grammar.Load
    member x.ContainingTypeName =
        let typeDef = x.MetadataReader.GetTypeDefinition x.DeclaringType
        x.MetadataReader.GetString typeDef.Name
    member x.ContainingTypeNamespace =
        let typeDef = x.MetadataReader.GetTypeDefinition x.DeclaringType
        x.MetadataReader.GetString typeDef.Namespace

module PrecompiledAssemblyFileLoader =

    let private typeNameCharactersToEscape = "\\[]+*&,"

    let escapeTypeName name =
        String.length name |> ignore
        if name.AsSpan().IndexOf typeNameCharactersToEscape >= 0 then
            let sb = StringBuilder()
            name
            |> String.iter (fun c ->
                if typeNameCharactersToEscape.Contains c then
                    sb.Append '\\' |> ignore
                sb.Append c |> ignore)
            sb.ToString()
        else
            name

    let private isNested flags =
        match flags &&& TypeAttributes.VisibilityMask with
        | TypeAttributes.NotPublic
        | TypeAttributes.Public -> true
        | _ -> false

    let getTypeFullName grammar =
        let md = grammar.MetadataReader
        grammar.DeclaringType
        |> List.unfold (fun t ->
            if t = Unchecked.defaultof<_> then
                None
            else
                let typ = md.GetTypeDefinition t
                let nextTyp = if isNested typ.Attributes then typ.GetDeclaringType() else Unchecked.defaultof<_>
                let ns = md.GetString typ.Namespace |> escapeTypeName
                let name = md.GetString typ.Name |> escapeTypeName
                if String.IsNullOrEmpty ns then
                    name
                else
                    $"{ns}.{name}"
                |> fun x -> x, nextTyp
                |> Some)
        |> List.rev
        |> String.concat "+"

    [<Literal>]
    let private PrecompiledGrammarAttributeNamespace = "Farkle.Runtime"

    [<Literal>]
    let private PrecompiledGrammarAttributeName = "PrecompiledGrammarAttribute"

    [<Literal>]
    let private ConstructorAttributes = MethodAttributes.SpecialName ||| MethodAttributes.RTSpecialName

    let private findPrecompiledGrammarAttributeConstructor(mr: MetadataReader) =
        let tryFindFromMemberReference() =
            mr.MemberReferences
            |> Seq.tryFind (fun m ->
                let m = mr.GetMemberReference m
                m.GetKind() = MemberReferenceKind.Method
                && mr.StringComparer.Equals(m.Name, ".ctor")
                && m.Parent.Kind = HandleKind.TypeReference
                && (let typeRef = TypeReferenceHandle.op_Explicit m.Parent |> mr.GetTypeReference
                    typeRef.ResolutionScope.Kind = HandleKind.AssemblyReference
                    && mr.StringComparer.Equals(mr.GetAssemblyReference(AssemblyReferenceHandle.op_Explicit typeRef.ResolutionScope).Name, "Farkle")
                    && mr.StringComparer.Equals(typeRef.Namespace, PrecompiledGrammarAttributeNamespace)
                    && mr.StringComparer.Equals(typeRef.Name, PrecompiledGrammarAttributeName)))
            |> Option.map (fun x -> MemberReferenceHandle.op_Implicit x : EntityHandle)
            |> Option.defaultValue Unchecked.defaultof<_>
        let tryFindFromSameType() =
            mr.MethodDefinitions
            |> Seq.tryFind (fun m ->
                let m = mr.GetMethodDefinition m
                m.Attributes &&& ConstructorAttributes = ConstructorAttributes
                && mr.StringComparer.Equals(m.Name, ".ctor")
                && (let declType = m.GetDeclaringType() |> mr.GetTypeDefinition
                    mr.StringComparer.Equals(declType.Namespace, PrecompiledGrammarAttributeNamespace)
                    && mr.StringComparer.Equals(declType.Name, PrecompiledGrammarAttributeName)))
            |> Option.map (fun x -> MethodDefinitionHandle.op_Implicit x : EntityHandle)
            |> Option.defaultValue Unchecked.defaultof<_>
        // Per the spec, the PrecompiledGrammarAttribute type can be declared at either
        // a Farkle assembly reference, or the input assembly itself.
        tryFindFromMemberReference()
        |> fun x -> if x.IsNil then tryFindFromSameType() else x

    // Decodes a signature by reading the layout size of a value type.
    // Returns 0 if the type is not supported.
    let private getStructSizeSignatureDecoder = {new ISignatureTypeProvider<int, unit> with
        member _.GetPrimitiveType typeCode: int =
            match typeCode with
            | PrimitiveTypeCode.Boolean | PrimitiveTypeCode.Byte | PrimitiveTypeCode.SByte -> 1
            | PrimitiveTypeCode.Char | PrimitiveTypeCode.Int16 | PrimitiveTypeCode.UInt16 -> 2
            | PrimitiveTypeCode.Int32 | PrimitiveTypeCode.UInt32 | PrimitiveTypeCode.Single -> 4
            | PrimitiveTypeCode.Int64 | PrimitiveTypeCode.UInt64 | PrimitiveTypeCode.Double -> 8
            | _ -> 0
        member _.GetTypeFromDefinition (reader, handle, rawTypeKind) =
            let kind = reader.ResolveSignatureTypeKind(handle, rawTypeKind)
            if kind = SignatureTypeKind.ValueType then
                reader.GetTypeDefinition(handle).GetLayout().Size
            else
                0
        member _.GetTypeFromReference (_, _, _) = 0
        member _.GetSZArrayType _ = 0
        member _.GetGenericInstantiation (_, _) = 0
        member _.GetArrayType (_, _) = 0
        member _.GetByReferenceType _ = 0
        member _.GetPointerType _ = 0
        member _.GetFunctionPointerType _ = 0
        member _.GetGenericMethodParameter (_, _) = 0
        member _.GetGenericTypeParameter (_, _) = 0
        member _.GetModifiedType (_, _, _) = 0
        member _.GetPinnedType _ = 0
        member _.GetTypeFromSpecification (_, _, _, _) = 0
    }

    exception StopCustomAttributeDecodeException

    let private dummyAttributeTypeProvider = {new ICustomAttributeTypeProvider<bool> with
        member _.GetPrimitiveType _ = false
        member _.GetTypeFromDefinition (_, _, _) = false
        member _.GetTypeFromReference (_, _, _) = false
        member _.GetSZArrayType _ = false
        member _.GetSystemType() = true
        member _.IsSystemType x = x
        member _.GetUnderlyingEnumType _ = raise StopCustomAttributeDecodeException
        member _.GetTypeFromSerializedName _ : bool = false
    }

    let private decodeNamedArguments (ca: CustomAttribute) =
        try
            ca.DecodeValue(dummyAttributeTypeProvider).NamedArguments
        with
        | StopCustomAttributeDecodeException -> ImmutableArray.Empty

    let loadAll (pe: PEReader) =
        if not pe.HasMetadata then
            []
        else
            let mr = pe.GetMetadataReader()
            let attrConstructor = findPrecompiledGrammarAttributeConstructor mr
            if attrConstructor.IsNil then
                []
            else
                mr.CustomAttributes
                |> Seq.choose (fun ca ->
                    let ca = mr.GetCustomAttribute ca
                    let interesting =
                        ca.Constructor = attrConstructor
                        && ca.Parent.Kind = HandleKind.FieldDefinition
                        && (let fld = FieldDefinitionHandle.op_Explicit ca.Parent |> mr.GetFieldDefinition
                            fld.Attributes &&& FieldAttributes.HasFieldRVA <> enum 0)
                    if not interesting then
                        None
                    else
                        let fldHandle = FieldDefinitionHandle.op_Explicit ca.Parent
                        let fld = mr.GetFieldDefinition fldHandle
                        let size = fld.DecodeSignature(getStructSizeSignatureDecoder, ())
                        if size = 0 then
                            None
                        else
                            let key =
                                decodeNamedArguments ca
                                |> Seq.tryFind (fun x -> x.Kind = CustomAttributeNamedArgumentKind.Property && x.Name = "Key" && x.Value :? string)
                                |> Option.map (fun x -> x.Value :?> string)
                                |> Option.toObj
                            Some {
                                PEFile = pe
                                MetadataReader = mr
                                RVA = fld.GetRelativeVirtualAddress()
                                Size = size
                                Field = fldHandle
                                DeclaringType = fld.GetDeclaringType()
                                Key = key
                            })
                |> List.ofSeq
