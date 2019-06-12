// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Templating

open Farkle
open Farkle.Grammar
open Farkle.Monads.Either
open Farkle.Tools
open System.Collections.Generic
open Scriban
open Scriban.Runtime
open System
open System.IO

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
    static member Create (g: Grammar.Grammar) =
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
    GrammarFile: string
}
with
    [<ScriptMemberIgnore>]
    static member Create grammar grammarFile bytes = {
        Farkle = FarkleObject.Create
        Grammar = grammar
        GrammarBytes = bytes
        GrammarFile = grammarFile
    }

module TemplateEngine =
    let createTemplateContext additionalProperties grammarFile = either {
        let tc = TemplateContext()
        tc.StrictVariables <- true
        let bytes = File.ReadAllBytes grammarFile
        let! grammar = GOLDParser.EGT.ofFile grammarFile |> Result.map Grammar.Create
        additionalProperties |> List.iter (fun (key, value) -> grammar.Properties.[key] <- value)
        let fr = FarkleRoot.Create grammar grammarFile bytes

        let so = ScriptObject()
        so.Import fr
        so.Import("to_base_64", Func<_,_>(Utilities.toBase64 fr.GrammarBytes))
        Utilities.load so

        tc.PushGlobal so
        let fFileExtension() =
            match so.TryGetValue("file_extension") with
            | (true, fExt) -> fExt.ToString()
            | (false, _) -> "out"
        return tc, fFileExtension
    }
