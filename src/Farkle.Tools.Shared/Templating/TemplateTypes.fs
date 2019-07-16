// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle.Grammar
open Farkle.Tools.Common
open Scriban.Runtime
open System
open System.Collections.Generic

type Symbols = {
    Terminals: Terminal[]
    Nonterminals: Nonterminal[]
    NoiseSymbols: Noise[]
}

type Grammar = {
    Properties: Dictionary<string,string>
    Symbols: Symbols
    Productions: Production[]
}
with
    [<ScriptMemberIgnore>]
    static member Create (g: Farkle.Grammar.Grammar) =
        let conv = Array.ofSeq
        let properties = Dictionary(g.Properties, StringComparer.OrdinalIgnoreCase)
        {
            Properties = properties
            Symbols = {
                Terminals = conv g.Symbols.Terminals
                Nonterminals = conv g.Symbols.Nonterminals
                NoiseSymbols = conv g.Symbols.NoiseSymbols
            }
            Productions = conv g.Productions
        }

type FarkleObject = {
    Version: string
}
with
    static member Create = {Version = toolsVersion}

type FarkleRoot = {
    Farkle: FarkleObject
    Grammar: Grammar
    [<ScriptMemberIgnore>]
    GrammarBytes: byte[]
    GrammarPath: string
}
with
    [<ScriptMemberIgnore>]
    static member Create grammar grammarPath bytes = {
        Farkle = FarkleObject.Create
        Grammar = grammar
        GrammarBytes = bytes
        GrammarPath = grammarPath
    }

type GeneratedTemplate = {
    FileExtension: string
    Content: string
}
