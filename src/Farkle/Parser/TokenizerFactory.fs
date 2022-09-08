// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Parser

open Farkle.Grammars
open System
#if NET
open System.Diagnostics.CodeAnalysis
#endif
open System.Runtime.CompilerServices

/// <summary>A class that creates <see cref="Tokenizer"/>s.</summary>
[<AbstractClass>]
type TokenizerFactory() =
    static let createTokenizerCallback =
        ConditionalWeakTable<_,Tokenizer>.CreateValueCallback(fun grammar -> DefaultTokenizer grammar :> _)
    // DefaultTokenizers are stateless and therefore
    // thread-safe. They can be made singletons.
    static let tokenizerCache = ConditionalWeakTable()
    static let defaultFactory =
        {new TokenizerFactory() with
            member _.CreateTokenizer grammar = tokenizerCache.GetValue(grammar, createTokenizerCallback)}
    /// <summary>Creates a <see cref="Tokenizer"/>.</summary>
    /// <param name="grammar">The <see cref="Grammar"/>
    /// for which the tokenizer is created.</param>
    abstract CreateTokenizer: grammar: Grammar -> Tokenizer
    /// <summary>A <see cref="TokenizerFactory"/> object that
    /// creates the default <see cref="Tokenizer"/>.</summary>
    static member internal Default = defaultFactory

[<Sealed>]
type internal TokenizerFactoryOfType(
#if NET
    [<DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)>]
#endif
    tokenizerType: Type) =
    inherit TokenizerFactory()
    // We can't make it a singleton because
    // of potential unloadability problems.
    let ctor = tokenizerType.GetConstructor([|typeof<Grammar>|])
    do
        if isNull ctor then
            raise (MissingMethodException(tokenizerType.FullName, ".ctor(Farkle.Grammars)"))
    override _.CreateTokenizer grammar =
        ctor.Invoke([|grammar|]) :?> _
