// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.PostProcessorCreator

open Farkle
open Farkle.Grammar
#if MODERN_FRAMEWORK
open System.Runtime.CompilerServices
open System.Threading
#endif

[<RequiresExplicitTypeArguments>]
let private createDefault<'T> (transformers: TransformerData []) (fusers: FuserData []) =
    let transformers = transformers |> Array.map (fun x -> x.BoxedDelegate)
    let fusers = fusers |> Array.map (fun x -> x.BoxedDelegate)
    {
        new PostProcessor<'T> with
            member _.Transform(Terminal(idx, _), context, data) =
                transformers.[int idx].Invoke(context, data)
            member _.Fuse(prod, members) =
                fusers.[int prod.Index].Invoke(members)
    }

#if MODERN_FRAMEWORK
/// A post-processor that starts using dynamic code on
/// supported platforms after being called certain times.
// Inspired by .NET's tiered compilation and by a similar
// trick Microsoft.Extensions.DependencyInjection does.
type private TieredPostProcessor<'T>(transformers, fusers) =
    let mutable ppImplementation = createDefault<'T> transformers fusers
    let mutable invokeCount = 1
    let mutable hasSwitchedToDynamic = 0
    [<Literal>]
    static let invocationThreshold = 3
    static let fSwitchToDynamic =
        WaitCallback(fun x -> (x :?> TieredPostProcessor<'T>).SwitchToDynamic())
    member private _.SwitchToDynamic() =
        if Interlocked.Exchange(&hasSwitchedToDynamic, 1) = 0 then
            try
                let dynamicPP = DynamicPostProcessor.create<'T> transformers fusers
                Volatile.Write(&ppImplementation, dynamicPP)
            with _ ->
#if DEBUG
                reraise()
#endif
                ()
    interface PostProcessor<'T> with
        member _.Transform(symbol, context, data) =
            ppImplementation.Transform(symbol, context, data)
        member _.Fuse(prod, members) =
            ppImplementation.Fuse(prod, members)
    interface PostProcessorEventListener with
        member this.ParsingStarted() =
            if Interlocked.Increment(&invokeCount) = invocationThreshold then
                ThreadPool.UnsafeQueueUserWorkItem(fSwitchToDynamic, this) |> ignore
#endif

[<RequiresExplicitTypeArguments>]
let create<'T> transformers fusers =
    #if MODERN_FRAMEWORK
    if RuntimeFeature.IsDynamicCodeCompiled then
        TieredPostProcessor<'T>(transformers, fusers) :> PostProcessor<_>
    else
    #endif
        createDefault<'T> transformers fusers
