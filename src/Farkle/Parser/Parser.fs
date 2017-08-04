// Copyright (c) 2017 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Chessie.ErrorHandling
open Farkle
open Farkle.Grammar
open Farkle.Monads.StateResult
open Farkle.Parser
open Farkle.Parser.Implementation
open System.IO
open System.Text

/// Functions that parse strings according to a specific grammar.
/// This is the definitive reference for all users of the library.
/// All the functions return a `Result<Reduction, ParseMessage>.
/// * If the function succeeds, its return value is the top reduction of the grammar.
/// * If it fails, the first message explains the reason it failed.
/// The rest of the messages are a kind of a log of the parser.
/// The term "trivial reductions" means a reduction that has only one nonterminal.
/// They can optionally be trimmed, so that the resulting parse tree is simplified.
type GOLDParser =

    /// Parses a parser state.
    /// This is the lowest-level method.
    /// ## Parameters
    /// * `state`: The parser state.
    static member ParseState state =
        let rec impl() = sresult {
            let! x = parse() |> liftState
            do! warn x
            match x with
            | Accept x ->
                do! warn <| Accept x
                return x
            | x when x |> ParseMessage.isError |> not ->
                do! warn x
                return! impl()
            | x ->
                return! x |> FatalError |> fail
        }
        eval (impl()) state

    static member private fbd x = Option.defaultValue false x

    /// Parses a string based on the given `Grammar` object, with an option to trim trivial reductions.
    /// Currently, the only way to create `Grammar`s is by using the functions in the `EGT` module.
    /// It's better to use this function if the same grammar is used multiple times, to avoid multiple grammars being constructed.
    /// ## Parameters
    /// * `grammar`: The `Grammar` object that contains the parsing logic.
    /// * `input`: The input string.
    /// * `trimReductions`: Whether the trivial reductions are trimmed.
    static member Parse (grammar, input, trimReductions) = input |> List.ofString |> ParserState.create trimReductions grammar |> GOLDParser.ParseState

    /// Parses a string based on the grammar on the given EGT file, with an option to trim trivial reductions.
    /// Please note that _only_ EGT files are supported, _not_ CGT files (an error will be raised in that case).
    /// ## Parameters
    /// * `egtFile`: The path of the EGTfile that contains the parsing logic.
    /// * `input`: The input string.
    /// * `trimReductions`: Whether the trivial reductions are trimmed.
    static member Parse (egtFile, input, trimReductions) = trial {
        let! grammar = EGT.ofFile egtFile |> Trial.mapFailure EGTReadError
        return! GOLDParser.Parse (grammar = grammar, input = input, trimReductions = trimReductions)
    }

    /// Parses a `Stream` based on the given `Grammar` object, with an option to trim trivial reductions.
    /// ## Parameters
    /// * `grammar`: The `Grammar` object that contains the parsing logic.
    /// * `inputStream`: The input stream.
    /// * `encoding`: The character encoding of the bytes in the stream.
    /// * `trimReductions`: Whether the trivial reductions are trimmed.
    static member Parse (grammar, inputStream, encoding: Encoding, trimReductions) = trial {
        let input = inputStream |> List.ofByteStream |> Array.ofList |> encoding.GetString
        return! GOLDParser.Parse (grammar = grammar, input = input, trimReductions = trimReductions)
    }
