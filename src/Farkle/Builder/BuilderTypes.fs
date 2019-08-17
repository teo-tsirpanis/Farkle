// Copyright (c) 2019 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle.Grammar

[<RequireQualifiedAccess>]
/// An error the builder encountered.
type BuildErrorType =
    /// Some symbols cannot be distinguished from each other.
    | IndistinguishableSymbols of DFASymbol Set
    /// Some symbols can contain zero characters.
    | NullableSymbols of DFASymbol Set
    /// No symbols were actually specified.
    | NoSymbolsSpecified
    override x.ToString() =
        match x with
        | IndistinguishableSymbols xs ->
            let symbols = xs |> Seq.map DFASymbol.toString |> String.concat ", "
            sprintf "Cannot distinguish between symbols: %s. \
The conflict is caused when two or more terminal definitions can accept the same text." symbols
        | NullableSymbols xs ->
            let symbols = xs |> Seq.map DFASymbol.toString |> String.concat ", "
            sprintf "The symbols %s can contain zero characters." symbols
        | NoSymbolsSpecified -> "No symbols were actually specified."
