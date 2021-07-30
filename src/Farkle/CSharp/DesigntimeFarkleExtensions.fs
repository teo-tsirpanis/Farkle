// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open Farkle.Builder
open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<Extension; AbstractClass; Sealed>]
/// Extension methods to create production builders.
type ProductionBuilderExtensions =
    [<Extension>]
    static member Appended(lit) = !& lit
    [<Extension>]
    static member Appended(df) = !% df
    [<Extension>]
    static member Extended(df) = !@ df
    [<Extension>]
    static member Finish(df, f) = (!@ df).Finish(f)
    [<Extension>]
    static member FinishConstant(str, constant) = !& str =% constant
    [<Extension>]
    static member AsIs(df) = (!@ df).AsIs()

[<Extension; AbstractClass; Sealed>]
/// <summary>Extension methods for <see cref="DesigntimeFarkle"/>s.</summary>
/// <remarks>Most of these methods must be applied to the topmost designtime Farkle
/// that will eventually be built. Use <see cref="Cast"/> first to use these functions
/// on untyped designtime Farkles.</remarks>
type DesigntimeFarkleExtensions =
    static member private GetOptionsOrDefault options =
        if Object.ReferenceEquals(options, null) then
            BuildOptions.Default
        else
            options
    [<Extension>]
    /// <summary>Casts a <see cref="DesigntimeFarkle"/>
    /// into a <see cref="DesigntimeFarkle{Object}"/></summary>
    /// <remarks>The object <paramref name="df"/> will return is undefined.</remarks>
    static member Cast df: [<Nullable(1uy, 2uy)>] _ = DesigntimeFarkle.cast df
    [<Extension>]
    /// <summary>Changes the name of a <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    /// <remarks>Useful for diagnostic purposes.</remarks>
    static member Rename<'T when 'T :> DesigntimeFarkle>(df: 'T, name) =
        DesigntimeFarkle.rename name df
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle{TResult}"/>
    /// into a <see cref="RuntimeFarkle{TResult}"/>.</summary>
    /// <param name="df">The designtime Farkle to build.</param>
    /// <param name="ct">Used to cancel the operation.</param>
    /// <param name="options">Additional options to configure the process.</param>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was triggered.</exception>
    /// <seealso cref="Farkle.RuntimeFarkle.build"/>
    static member Build<[<Nullable(0uy)>] 'TResult>(df: DesigntimeFarkle<'TResult>, [<Optional>] ct, [<Optional; Nullable(2uy)>] options) =
        let options = DesigntimeFarkleExtensions.GetOptionsOrDefault options
        let grammar, pp = DesigntimeFarkleBuild.buildEx ct options df
        RuntimeFarkle.CreateMaybe pp grammar
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle"/> into a syntax-checking
    /// <see cref="RuntimeFarkle{System.Object}"/>.</summary>
    /// <param name="df">The designtime Farkle to build.</param>
    /// <param name="ct">Used to cancel the operation.</param>
    /// <param name="options">Additional options to configure the process.</param>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was triggered.</exception>
    /// <seealso cref="Farkle.RuntimeFarkle.buildUntyped"/>
    static member BuildUntyped(df, [<Optional>] ct, [<Optional; Nullable(2uy)>] options) =
        let options = DesigntimeFarkleExtensions.GetOptionsOrDefault options
        let grammar =
            df
            |> DesigntimeFarkleBuild.createGrammarDefinition
            |> DesigntimeFarkleBuild.buildGrammarOnlyEx ct options
        RuntimeFarkle.CreateMaybe RuntimeFarkle.syntaxCheckerObj grammar
    [<Extension>]
    /// <summary>Sets a custom <see cref="GrammarMetadata"/>
    /// object to a <see cref="DesigntimeFarkle"/>.</summary>
    /// <seealso cref="WithOperatorScope"/>
    /// <seealso cref="CaseSensitive"/>
    /// <seealso cref="AutoWhitespace"/>
    /// <seealso cref="AddNoiseSymbol"/>
    /// <seealso cref="AddLineComment"/>
    /// <seealso cref="AddBlockComment"/>
    static member WithMetadata<'T when 'T :> DesigntimeFarkle>(df: 'T, metadata) =
        DesigntimeFarkle.withMetadata metadata df
    [<Extension>]
    /// <summary>Sets an <see cref="Farkle.Builder.OperatorPrecedence.OperatorScope"/>
    /// to a <see cref="DesigntimeFarkle"/>. Any previous ones -if exist- are discarded.</summary>
    static member WithOperatorScope<'T when 'T :> DesigntimeFarkle>(df: 'T, opScope) =
        DesigntimeFarkle.withOperatorScope opScope df
    [<Extension>]
    /// <summary>Controls whether the given <see cref="DesigntimeFarkle"/>
    /// is case sensitive.</summary>
    static member CaseSensitive<'T when 'T :> DesigntimeFarkle>(df: 'T, [<Optional; DefaultParameterValue(true)>] x) =
        DesigntimeFarkle.caseSensitive x df
    [<Extension>]
    /// <summary>Controls whether the given <see cref="DesigntimeFarkle"/>
    /// would automatically ignore whitespace in input text.</summary>
    static member AutoWhitespace<'T when 'T :> DesigntimeFarkle>(df: 'T, x) =
        DesigntimeFarkle.autoWhitespace x df
    [<Extension>]
    /// <summary>Adds a symbol specified by the given <see cref="Regex"/>
    /// that will be ignored in input text, to the given <see cref="DesigntimeFarkle"/>.</summary>
    static member AddNoiseSymbol<'T when 'T :> DesigntimeFarkle>(df: 'T, name, regex) =
        DesigntimeFarkle.addNoiseSymbol name regex df
    [<Extension>]
    /// <summary>Adds a line comment to the given
    /// <see cref="DesigntimeFarkle"/>.</summary>
    static member AddLineComment<'T when 'T :> DesigntimeFarkle>(df: 'T, commentStart) =
        DesigntimeFarkle.addLineComment commentStart df
    [<Extension>]
    /// <summary>Adds a block comment to the given
    /// <see cref="DesigntimeFarkle"/>.</summary>
    static member AddBlockComment<'T when 'T :> DesigntimeFarkle>(df: 'T, commentStart, commentEnd) =
        DesigntimeFarkle.addBlockComment commentStart commentEnd df
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TResult}"/>
    /// that transforms the output of the given one with the given delegate.</summary>
    static member Select<[<Nullable(0uy)>] 'TResult, [<Nullable(0uy)>] 'TConverted>
        (df: DesigntimeFarkle<'TResult>, f): DesigntimeFarkle<'TConverted> =
        let name = sprintf "%s %s" df.Name typeof<'TConverted>.Name
        name ||= [(!@ df).Finish(f)]
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TCollection}"/>
    /// that recognizes many occurrences of the given
    /// <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    /// <param name="df">The designtime Farkle to recognize many times.</param>
    /// <param name="atLeastOne">Whether at least one occurrence
    /// is required. Defaults to false.</param>
    /// <typeparam name="TCollection">The type of the collection to
    /// store the results. It must implement
    /// <see cref="System.Collections.Generic.ICollection{TResult}"/>
    /// and have a parameterless constructor.</typeparam>
    /// <seealso cref="SeparatedBy"/>
    static member Many<[<Nullable(0uy)>] 'TResult, 'TCollection
        when 'TCollection :> ICollection<'TResult> and 'TCollection: (new: unit -> 'TCollection)>
        (df: DesigntimeFarkle<'TResult>, [<Optional; DefaultParameterValue(false)>] atLeastOne)
        : DesigntimeFarkle<'TCollection> =
        if atLeastOne then
            manyCollection1 df
        else
            manyCollection df
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TCollection}"/>
    /// that recognizes many occurrences of the given
    /// <see cref="DesigntimeFarkle{TResult}"/>, separated by another
    /// <see cref="DesigntimeFarkle"/>.</summary>
    /// <param name="df">The designtime Farkle to recognize many times.</param>
    /// <param name="separator">The designtime Farkle that
    /// separates instances of <paramref name="df"/>.</param>
    /// <param name="atLeastOne">Whether at least one occurrence
    /// is required. Defaults to false.</param>
    /// <typeparam name="TCollection">The type of the collection to
    /// store the results. It must implement
    /// <see cref="System.Collections.Generic.ICollection{TResult}"/>
    /// and have a parameterless constructor.</typeparam>
    /// <seealso cref="Many"/>
    static member SeparatedBy<[<Nullable(0uy)>] 'TResult, 'TCollection
        when 'TCollection :> ICollection<'TResult> and 'TCollection: (new: unit -> 'TCollection)>
        (df: DesigntimeFarkle<'TResult>, separator, [<Optional; DefaultParameterValue(false)>] atLeastOne)
        : DesigntimeFarkle<'TCollection> =
        if atLeastOne then
            sepByCollection1 separator df
        else
            sepByCollection separator df
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TResult}"/>
    /// that might recognize the given one, or not. In the latter
    /// case, it returns the default value of the result type.</summary>
    /// <seealso cref="Nullable"/>
    static member Optional<[<Nullable(2uy)>] 'TResult>(df: DesigntimeFarkle<'TResult>) : [<Nullable(1uy, 2uy)>] DesigntimeFarkle<'TResult> =
        Nonterminal.Create(sprintf "%s Maybe" df.Name,
            df.Extended().AsIs(),
            ProductionBuilder.Empty.FinishConstant(Unchecked.defaultof<'TResult>))
    [<Extension>]
    /// <seealso cref="Optional"/>
    static member Nullable(df: DesigntimeFarkle<'TResult>) =
        Nonterminal.Create(sprintf "%s Maybe" df.Name,
            df.Extended().Finish(Nullable.op_Implicit),
            ProductionBuilder.Empty.FinishConstant(Nullable()))
