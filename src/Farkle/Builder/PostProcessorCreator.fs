// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

module internal Farkle.Builder.PostProcessorCreator

open Farkle
open Farkle.Grammars

[<RequiresExplicitTypeArguments>]
let private createDefault<'T> (transformers: TransformerData []) (fusers: FuserData []) =
    let transformers = transformers |> Array.map (fun x -> x.BoxedDelegate)
    let fusers = fusers |> Array.map (fun x -> x.BoxedDelegate)
    {
        new IPostProcessor<'T> with
            member _.Transform(Terminal(idx, _), context, data) =
                transformers.[int idx].Invoke(context, data)
            member _.Fuse(prod, members) =
                fusers.[int prod.Index].Invoke(members)
    }

[<RequiresExplicitTypeArguments>]
let internal create<'T> dfDef =
    let transformers =
        let arr = Array.zeroCreate dfDef.TerminalEquivalents.Count
        for i = 0 to arr.Length - 1 do
            arr.[i] <-
                let (Named(_, te)) = dfDef.TerminalEquivalents.[i]
                match te with
                | TerminalEquivalent.Terminal term -> term.Transformer
                | TerminalEquivalent.LineGroup lg -> lg.Transformer
                | TerminalEquivalent.BlockGroup bg -> bg.Transformer
                | TerminalEquivalent.Literal _
                | TerminalEquivalent.NewLine
                | TerminalEquivalent.VirtualTerminal _ -> TransformerData.Null
        arr
    let fusers =
        let arr = Array.zeroCreate dfDef.Productions.Count
        for i = 0 to arr.Length - 1 do
            let _, prod = dfDef.Productions.[i]
            arr.[i] <- prod.Fuser
        arr

    let ppFactory = dfDef.Metadata.PostProcessorFactory
    if ppFactory.IsNull then
        createDefault<'T> transformers fusers
    else
        ppFactory.ValueUnchecked.CreatePostProcessor(transformers, fusers, typeof<'T>) :?> _
