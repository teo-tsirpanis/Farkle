// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.PostProcessorCreator

open Farkle
open System.Collections.Immutable

[<RequiresExplicitTypeArguments>]
let private createDefault<'T> (transformers: ImmutableArray<T<obj>>) (fusers: ImmutableArray<F<obj>>) =
    {
        new PostProcessor<'T> with
            member _.Transform(term, context, data) =
                transformers.[int term.Index].Invoke(context, data)
            member _.Fuse(prod, members) =
                fusers.[int prod.Index].Invoke(members)
    }

// Turns out that statically calling a delegate's
// method is not as easy as previously thought.
// The emitter code will be pseudo-commented for now.
#if !NETSTANDARD2_0
open System
open System.Collections.Generic
open System.Reflection
open System.Reflection.Emit
open System.Runtime.CompilerServices
open System.Threading

[<Literal>]
let private fldPrivateReadonly = FieldAttributes.Private ||| FieldAttributes.InitOnly

let mutable generatedPostProcessorIndex = 0L

let private getTargetArray (xs: #Delegate ImmutableArray) =
    let arr = Array.zeroCreate xs.Length
    for i = 0 to arr.Length - 1 do
        arr.[i] <- xs.[i].Target
    arr

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

let getTransformLdargs = [|OpCodes.Ldarg_2; OpCodes.Ldarg_3|]

let getFuseLdargs = [|OpCodes.Ldarg_2|]

let createIgnoresAccessChecksToAttribute (_mod: ModuleBuilder) =
    let attrType = _mod.DefineType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", TypeAttributes.Public, typeof<Attribute>)

    let ctor = attrType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, justStringCtorParameters)
    ctor.GetILGenerator().Emit(OpCodes.Ret)
    attrType.CreateType().GetConstructor(justStringCtorParameters)

[<RequiresExplicitTypeArguments>]
let private emitPPMethod<'TSymbol, 'TDelegate when 'TDelegate :> Delegate> methodName argTypes fAssembly
    (ldArgOpCodes: _ []) (targetsFld: FieldInfo) (delegates: ImmutableArray<'TDelegate>) (ppType: TypeBuilder) =

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
        let d = delegates.[i]
        fAssembly d.Method.Module.Assembly
        ilg.MarkLabel(labels.[i])
        // If the method is an instance method, we push that instance to the stack.
        // Because IL does not support literal objects, we pass these objects through
        // the constructor
        if not d.Method.IsStatic then
            ilg.Emit(OpCodes.Ldloc, targetsLocal)
            ilg.Emit(OpCodes.Ldloc, indexLocal)
            ilg.Emit(OpCodes.Ldelem_Ref)

        // We push the method's other arguments.
        // They vary, depending on whether we have a transformer of a fuser.
        for opCode in ldArgOpCodes do
            ilg.Emit(opCode)
        // And now we call the delegate's method, which is statically resolved.
        // Tail calls have caused performance issues in the past and might prevent method inlining.
        // ilg.Emit(OpCodes.Tailcall)
        let callOpCode = if d.Method.IsStatic then OpCodes.Call else OpCodes.Callvirt
        ilg.Emit(callOpCode, d.Method)
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

    emitPPMethod<Grammar.Terminal,T<obj>>
        "Transform" transformParameters fNewAssembly getTransformLdargs transformerTargetsFld transformers ppType
    emitPPMethod<Grammar.Production,F<obj>>
        "Fuse" fuseParameters fNewAssembly getFuseLdargs fuserTargetsFld fusers ppType

    let transformerTargets = getTargetArray transformers
    let fuserTargets = getTargetArray fusers

    let ppTypeReal = ppType.CreateType()
    let ppCtorReal = ppTypeReal.GetConstructor(ppCtorParameters)
    ppCtorReal.Invoke([|transformerTargets; fuserTargets|]) :?> PostProcessor<'T>

#endif

[<RequiresExplicitTypeArguments>]
let create<'T> (useDynamicCode: bool) transformers fusers =
    #if !NETSTANDARD2_0
    if RuntimeFeature.IsDynamicCodeCompiled && useDynamicCode then
        createDynamic<'T> transformers fusers
    else
    #endif
        createDefault<'T> transformers fusers
