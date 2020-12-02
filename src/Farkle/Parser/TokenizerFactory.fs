// Copyright (c) 2020 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Grammar
open System

/// <summary>A class that creates <see cref="Tokenizer"/>s.</summary>
type TokenizerFactory() =
    static let instance = TokenizerFactory()
    /// <summary>Creates a <see cref="Tokenizer"/>.</summary>
    /// <param name="grammar">The <see cref="Grammar"/>
    /// for which the tokenizer is created.</param>
    abstract CreateTokenizer: grammar: Grammar -> Tokenizer
    default _.CreateTokenizer grammar = Tokenizer(grammar)
    /// <summary>A <see cref="TokenizerFactory"/> object that
    /// creates the default <see cref="Tokenizer"/>.</summary>
    static member internal Default = instance

[<Sealed>]
type internal TokenizerFactoryOfType(tokenizerType: Type) =
    inherit TokenizerFactory()
    // We can't make it a singleton because 
    // of potential unloadability problems.
    let ctor = tokenizerType.GetConstructor([|typeof<Grammar>|])
    do
        if isNull ctor then
            raise (MissingMethodException(tokenizerType.FullName, ".ctor(Farkle.Grammar)"))
    override _.CreateTokenizer grammar =
        ctor.Invoke([|grammar|]) :?> _
