// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.PostProcessorCreator

open Farkle

[<RequiresExplicitTypeArguments>]
let private createDefault<'T> (transformers: TransformerData []) (fusers: FuserData []) =
    let transformers = transformers |> Array.map (fun x -> x.BoxedDelegate)
    let fusers = fusers |> Array.map (fun x -> x.BoxedDelegate)
    {
        new PostProcessor<'T> with
            member _.Transform(term, context, data) =
                transformers.[int term.Index].Invoke(context, data)
            member _.Fuse(prod, members) =
                fusers.[int prod.Index].Invoke(members)
    }

#if MODERN_FRAMEWORK
open System
open System.Collections.Generic
open System.Reflection
open System.Reflection.Emit
open System.Runtime.CompilerServices
open System.Threading

// We can't directly access ReadOnlySpan's indexer due to
// https://github.com/dotnet/runtime/issues/45283
[<AbstractClass; Sealed>]
type private ReadOnlySpanOfObjectIndexer =
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member GetItem(span: byref<ReadOnlySpan<obj>>, idx) =
        &span.[idx]

[<Literal>]
let private fldPrivateReadonly = FieldAttributes.Private ||| FieldAttributes.InitOnly

let mutable generatedPostProcessorIndex = 0L

let private getParameterTypes (xs: ParameterInfo []) =
    xs |> Array.map (fun p -> p.ParameterType)

let private argOutOfRangeCtor =
    typeof<ArgumentOutOfRangeException>.GetConstructor([|typeof<string>|])

let private justStringCtorParameters = [|typeof<string>|]

let private ppCtorParameters = Array.replicate 2 typeof<obj[]>

let private transformParameters =
    typeof<ITransformer<Grammar.Terminal>>.GetMethod("Transform").GetParameters()
    |> getParameterTypes

let private fuseParameters =
    typeof<PostProcessor<obj>>.GetMethod("Fuse").GetParameters()
    |> getParameterTypes

let private stringFormatMethodOneObj =
    typeof<string>.GetMethod("Format", [|typeof<string>; typeof<obj>|])

let private readOnlySpanOfObjectIndexer =
    // Type.GetType("System.ReadOnlySpan`1").MakeGenericType(typeof<obj>).GetProperty("Item").GetGetMethod()
    typeof<ReadOnlySpanOfObjectIndexer>.GetMethod("GetItem", BindingFlags.NonPublic ||| BindingFlags.Static)

let private createIgnoresAccessChecksToAttribute (_mod: ModuleBuilder) =
    let attrType = _mod.DefineType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", TypeAttributes.Public, typeof<Attribute>)

    let ctor = attrType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, justStringCtorParameters)
    ctor.GetILGenerator().Emit(OpCodes.Ret)
    attrType.CreateType().GetConstructor(justStringCtorParameters)

[<RequiresExplicitTypeArguments>]
let private emitPPMethod<'TSymbol, 'TDelegate when 'TDelegate :> IRawDelegateProvider> methodName argTypes fAssembly
    fIsSpecialCase fEmitArgs delegateArgCount (targetFields: FieldInfo option [])
    (delegates: 'TDelegate []) (ppType: TypeBuilder) =

    // Interface implementation methods have to be virtual.
    let method = ppType.DefineMethod(methodName, MethodAttributes.Public ||| MethodAttributes.Virtual, typeof<obj>, argTypes)
    let ilg = method.GetILGenerator()

    // int index;
    let indexLocal = ilg.DeclareLocal(typeof<int>)
    let labels = Array.init delegates.Length (fun _ -> ilg.DefineLabel())
    let indexOutOfRangeLabel = ilg.DefineLabel()

    // index = symbol.Index;
    ilg.Emit(OpCodes.Ldarg_1)
    ilg.Emit(OpCodes.Callvirt, typeof<'TSymbol>.GetProperty("Index").GetGetMethod())
    ilg.Emit(OpCodes.Stloc, indexLocal)

    // We branch depending on the index.
    ilg.Emit(OpCodes.Ldloc, indexLocal)
    ilg.Emit(OpCodes.Switch, labels)
    // If the index is not recognized we jump to the exception-throwing path at the end.
    ilg.Emit(OpCodes.Br, indexOutOfRangeLabel)

    for i = 0 to delegates.Length - 1 do
        ilg.MarkLabel(labels.[i])

        let d = delegates.[i]
        if fIsSpecialCase d ilg then
            ()
        elif d.IsConstant then
            match targetFields.[i] with
            | None -> ilg.Emit(OpCodes.Ldnull)
            | Some fld ->
                ilg.Emit(OpCodes.Ldarg_0)
                ilg.Emit(OpCodes.Ldfld, fld)
        elif d.IsNull then
            ilg.Emit(OpCodes.Ldnull)
        else
            let dMethod = d.RawDelegate.Method
            fAssembly dMethod.Module.Assembly

            // We have to push the delegate's target if the delegate represents
            // an instance method or a static method closed over its first argument.
            // Merely checking the target object for null is not a good idea; the
            // runtime allows delegates of instance methods with null targets.
            let targetType =
                match dMethod.IsStatic, dMethod.GetParameters() with
                | false, _ -> dMethod.DeclaringType
                | true, methParams when methParams.Length = delegateArgCount + 1 ->
                    methParams.[0].ParameterType
                | true, _ -> null
            if not (isNull targetType) then
                match targetFields.[i] with
                | None -> ilg.Emit(OpCodes.Ldnull)
                | Some fld ->
                    ilg.Emit(OpCodes.Ldarg_0)
                    ilg.Emit(OpCodes.Ldfld, fld)
                    if targetType.IsValueType then
                        // If the target is a struct we have to unbox it.
                        // We use the unbox opcode, returning a reference
                        // to the boxed struct which is passed to the method
                        // (remember that struct instance methods take a
                        // reference to that struct). This boxed struct can
                        // be modified, just like how delegates behave.
                        ilg.Emit(OpCodes.Unbox, targetType)

            // We push the method's other arguments.
            // They vary, depending on whether we have a transformer of a fuser.
            fEmitArgs d ilg

            // And now we call the delegate's method, which is statically resolved.
            // Tail calls have caused performance issues in the past and might prevent method inlining.
            // ilg.Emit(OpCodes.Tailcall)

            // We always use call. It has been tested that the method
            // is devirtualized when the delegate gets created. We don't
            // care about the null check callvirt provides. We can create
            // delegates from instance methods with a null target. When they
            // are caled, they don't NRE until that null target is attempted to be actually used.
            ilg.Emit(OpCodes.Call, dMethod)
            if d.ReturnType.IsValueType then
                ilg.Emit(OpCodes.Box, d.ReturnType)

        ilg.Emit(OpCodes.Ret)

    ilg.MarkLabel(indexOutOfRangeLabel)
    // We customize the error message in two stages.
    // First, we statically write whether we have a
    // terminal or production and the maximum allowed index.
    // And second we use String.Format to dynamically place
    // the index's value in the error message.
    let errorMessage =
        sprintf "Invalid %s index; expected to be between 0 and %d but was {0}."
            (typeof<'TSymbol>.Name.ToLowerInvariant())
            (labels.Length - 1)
    ilg.Emit(OpCodes.Ldstr, errorMessage)
    ilg.Emit(OpCodes.Ldloc, indexLocal)
    ilg.Emit(OpCodes.Box, typeof<int>)
    ilg.Emit(OpCodes.Call, stringFormatMethodOneObj)
    ilg.Emit(OpCodes.Newobj, argOutOfRangeCtor)
    ilg.Emit(OpCodes.Throw)

let private createDelegateTargetFields (typeBuilder: TypeBuilder) (ctorIlg: ILGenerator)
    (targets: obj[]) (argIdx: int16) namePrefix =
    let fields = Array.zeroCreate targets.Length
    let fieldDigitCount =
        targets.Length |> float |> Math.Log10 |> Math.Ceiling |> int

    // We load the fields in descending order to avoid many range checks.
    for i = targets.Length - 1 downto 0 do
        match targets.[i] with
        | null -> ()
        | target ->
            // By prepending zeroes, the debugger vill show the fields in order.
            let fieldName = sprintf "_%sTarget%0*d" namePrefix fieldDigitCount i
            let fieldType =
                match target.GetType() with
                // We will store value-typed targets in their boxed form.
                // It will keep the transformers
                | x when x.IsValueType -> typeof<obj>
                | x -> x
            let field = typeBuilder.DefineField(fieldName, fieldType, fldPrivateReadonly) :> FieldInfo

            ctorIlg.Emit(OpCodes.Ldarg_0)
            ctorIlg.Emit(OpCodes.Ldarg, argIdx)
            ctorIlg.Emit(OpCodes.Ldc_I4, i)
            ctorIlg.Emit(OpCodes.Ldelem_Ref)
            if not fieldType.IsValueType then
                ctorIlg.Emit(OpCodes.Castclass, fieldType)
            ctorIlg.Emit(OpCodes.Stfld, field)

            fields.[i] <- Some field

    fields

[<RequiresExplicitTypeArguments>]
let private createDynamic<'T> (transformers: TransformerData []) (fusers: FuserData []) =
    // I am not sure of the effects of name collisions in dynamic assemblies but we will
    // nevertheless name each generated assembly with a different name. A GUID would have
    // been an easier solution but it is long and has special characters.
    let ppIdx = Interlocked.Increment(&generatedPostProcessorIndex)
    let name = sprintf "Farkle.DynamicCodeAssembly%d" ppIdx
    let asmName = AssemblyName(name)
    let asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect)
    let mainModule = asmBuilder.DefineDynamicModule("MainModule")

    let ctorIgnoreAccessChecksTo = createIgnoresAccessChecksToAttribute mainModule

    let fNewAssembly =
        let cache = HashSet(StringComparer.Ordinal)
        fun (asm: Assembly) ->
            // Passing the assembly's full name to the attribute fails.
            let name = asm.GetName().Name
            if cache.Add(name) then
                asmBuilder.SetCustomAttribute(CustomAttributeBuilder(ctorIgnoreAccessChecksTo, [|name|]))

    let ppType = mainModule.DefineType("DynamicCodePostProcessor", TypeAttributes.Sealed)
    ppType.AddInterfaceImplementation(typeof<ITransformer<Grammar.Terminal>>)
    ppType.AddInterfaceImplementation(typeof<PostProcessor<'T>>)

    let transformerTargets =
        transformers
        |> Array.map (fun x -> x.RawDelegate.Target)
    let fuserTargets =
        fusers
        |> Array.map (fun x ->
            match x.Constant with
            | ValueSome x -> x
            | ValueNone -> x.RawDelegate.Target)

    let transformerTargetFields, fuserTargetFields =
        let ctor = ppType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, ppCtorParameters)
        let ilg = ctor.GetILGenerator()
        let transformerTargetFields = createDelegateTargetFields ppType ilg transformerTargets 1s "transformer"
        let fuserTargetFields = createDelegateTargetFields ppType ilg fuserTargets 2s "fuser"

        ilg.Emit(OpCodes.Ret)

        transformerTargetFields, fuserTargetFields

    emitPPMethod<Grammar.Terminal,TransformerData>
        "Transform" transformParameters fNewAssembly
        (fun _ _ -> false)
        (fun _ ilg ->
            ilg.Emit(OpCodes.Ldarg_2)
            ilg.Emit(OpCodes.Ldarg_3))
        2 transformerTargetFields transformers ppType

    fNewAssembly typeof<ReadOnlySpanOfObjectIndexer>.Assembly
    emitPPMethod<Grammar.Production,FuserData>
        "Fuse" fuseParameters fNewAssembly
        (fun d ilg ->
            match d.Parameters with
            | [idx, _] when d.IsAsIs ->
                ilg.Emit(OpCodes.Ldarga_S, 2uy)
                ilg.Emit(OpCodes.Ldc_I4, idx)
                ilg.Emit(OpCodes.Call, readOnlySpanOfObjectIndexer)
                ilg.Emit(OpCodes.Ldind_Ref)
                true
            | _ -> false)
        (fun d ilg ->
            if d.IsRaw then
                ilg.Emit(OpCodes.Ldarg_2)
            else
                for (idx, typ) in d.Parameters do
                    ilg.Emit(OpCodes.Ldarga_S, 2uy)
                    ilg.Emit(OpCodes.Ldc_I4, idx)
                    ilg.Emit(OpCodes.Call, readOnlySpanOfObjectIndexer)
                    ilg.Emit(OpCodes.Ldind_Ref)
                    ilg.Emit(OpCodes.Unbox_Any, typ))
        1 fuserTargetFields fusers ppType

    let ppTypeReal = ppType.CreateType()
    let ppCtorReal = ppTypeReal.GetConstructor(ppCtorParameters)
    ppCtorReal.Invoke([|transformerTargets; fuserTargets|]) :?> PostProcessor<'T>

#endif

[<RequiresExplicitTypeArguments>]
let create<'T> (useDynamicCode: bool) transformers fusers =
    #if MODERN_FRAMEWORK
    if RuntimeFeature.IsDynamicCodeCompiled && useDynamicCode then
        createDynamic<'T> transformers fusers
    else
    #endif
        createDefault<'T> transformers fusers
