// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.PostProcessorCreator

open Farkle
open System.Collections.Immutable

[<RequiresExplicitTypeArguments>]
let private createDefault<'T> (transformers: ImmutableArray<TransformerData>) (fusers: ImmutableArray<FuserData>) =
    {
        new PostProcessor<'T> with
            member _.Transform(term, context, data) =
                transformers.[int term.Index].BoxedDelegate.Invoke(context, data)
            member _.Fuse(prod, members) =
                fusers.[int prod.Index].BoxedDelegate.Invoke(members)
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
    fIsSpecialCase fEmitArgs delegateArgCount (targetsFld: FieldInfo)
    (delegates: ImmutableArray<'TDelegate>) (ppType: TypeBuilder) =

    // Interface implementation methods have to be virtual.
    let method = ppType.DefineMethod(methodName, MethodAttributes.Public ||| MethodAttributes.Virtual, typeof<obj>, argTypes)
    let ilg = method.GetILGenerator()

    let getIndexMethod = typeof<'TSymbol>.GetProperty("Index").GetGetMethod()
    // object[] targets;
    let targetsLocal = ilg.DeclareLocal(typeof<obj[]>)
    // int index;
    let indexLocal = ilg.DeclareLocal(typeof<int>)
    let labels = Array.init delegates.Length (fun _ -> ilg.DefineLabel())
    let indexOutOfRangeLabel = ilg.DefineLabel()
    // targets = this._***Targets;
    ilg.Emit(OpCodes.Ldarg_0)
    ilg.Emit(OpCodes.Ldfld, targetsFld)
    ilg.Emit(OpCodes.Stloc, targetsLocal)

    // index = symbol.Index;
    ilg.Emit(OpCodes.Ldarg_1)
    ilg.Emit(OpCodes.Callvirt, getIndexMethod)
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
            ilg.Emit(OpCodes.Ldloc, targetsLocal)
            ilg.Emit(OpCodes.Ldloc, indexLocal)
            ilg.Emit(OpCodes.Ldelem_Ref)
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
                ilg.Emit(OpCodes.Ldloc, targetsLocal)
                ilg.Emit(OpCodes.Ldloc, indexLocal)
                ilg.Emit(OpCodes.Ldelem_Ref)
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

[<RequiresExplicitTypeArguments>]
let private createDynamic<'T> transformers fusers =
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

    let transformerTargetsFld = ppType.DefineField("_transformerTargets", typeof<obj[]>, fldPrivateReadonly)
    let fuserTargetsFld = ppType.DefineField("_fuserTargets", typeof<obj[]>, fldPrivateReadonly)
    do
        let ctor = ppType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, ppCtorParameters)

        let ilg = ctor.GetILGenerator()
        ilg.Emit(OpCodes.Ldarg_0)
        ilg.Emit(OpCodes.Ldarg_1)
        ilg.Emit(OpCodes.Stfld, transformerTargetsFld)
        ilg.Emit(OpCodes.Ldarg_0)
        ilg.Emit(OpCodes.Ldarg_2)
        ilg.Emit(OpCodes.Stfld, fuserTargetsFld)
        ilg.Emit(OpCodes.Ret)

    emitPPMethod<Grammar.Terminal,TransformerData>
        "Transform" transformParameters fNewAssembly
        (fun _ _ -> false)
        (fun _ ilg ->
            ilg.Emit(OpCodes.Ldarg_2)
            ilg.Emit(OpCodes.Ldarg_3))
        2 transformerTargetsFld transformers ppType

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
        1 fuserTargetsFld fusers ppType

    let transformerTargets =
        transformers
        |> Seq.map (fun x -> x.RawDelegate.Target)
        |> Array.ofSeq
    let fuserTargets =
        fusers
        |> Seq.map (fun x ->
            match x.Constant with
            | ValueSome x -> x
            | ValueNone -> x.RawDelegate.Target)
        |> Array.ofSeq

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
