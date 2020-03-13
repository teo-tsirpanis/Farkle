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
/// <summary>Extension methods for the <see cref="DesigntimeFarkle{TResult}"/> type.</summary>
type DesigntimeFarkleExtensions =
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle{TResult}"/> into a <see cref="RuntimeFarkle{TResult}"/>.</summary>
    static member Build (df: DesigntimeFarkle<'TResult>) = RuntimeFarkle.build df
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle"/> into a syntax-checking
    /// <see cref="RuntimeFarkle{System.Object}"/>.</summary>
    static member BuildUntyped df = RuntimeFarkle.buildUntyped(df).SyntaxCheck()
    [<Extension>]
    /// <summary>Controls whether the given <see cref="DesigntimeFarkle{TResult}"/>
    /// is case sensitive.</summary>
    static member CaseSensitive (df: DesigntimeFarkle<'TResult>, [<Optional; DefaultParameterValue(true)>] x) =
        DesigntimeFarkle.caseSensitive x df
    [<Extension>]
    /// <summary>Controls whether the given <see cref="DesigntimeFarkle{TResult}"/>
    /// would automatically ignore whitespace in input text.</summary>
    static member AutoWhitespace (df: DesigntimeFarkle<'TResult>, x) =
        DesigntimeFarkle.autoWhitespace x df
    [<Extension>]
    /// <summary>Adds a symbol specified by the given <see cref="Regex"/>
    /// that will be ignored in input text.</summary>
    static member AddNoiseSymbol (df: DesigntimeFarkle<'TResult>, name, regex) =
        DesigntimeFarkle.addNoiseSymbol name regex df
    [<Extension>]
    /// <summary>Adds a new line comment in the given
    /// <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    static member AddLineComment (df: DesigntimeFarkle<'TResult>, commentStart) =
        DesigntimeFarkle.addLineComment commentStart df
    [<Extension>]
    /// <summary>Adds a new block comment in the given
    /// <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    static member AddBlockComment (df: DesigntimeFarkle<'TResult>, commentStart, commentEnd) =
        DesigntimeFarkle.addBlockComment commentStart commentEnd df
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TResult}"/>
    /// that transforms the output of the given one with the given delegate.</summary>
    static member Select(df: DesigntimeFarkle<'TResult>, f): DesigntimeFarkle<'TConverted> =
        df |>> (FuncConvert.FromFunc<_,_> f)
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TCollection}"/>
    /// that recognizes many occurrences of the given
    /// <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    /// <param name="atLeastOne">Whether at least one occurrence
    /// is required. Defaults to false.</param>
    /// <typeparam name="TCollection">The type of the collection to
    /// store the results. It must implement
    /// <see cref="System.Collections.Generic.ICollection{TResult}"/>
    /// and have a parameterless constructor.</typeparam>
    /// <seealso cref="SeparatedBy"/>
    static member Many (df: DesigntimeFarkle<'TResult>,
        [<Optional; DefaultParameterValue(false)>] atLeastOne): DesigntimeFarkle<'TCollection> =
        if atLeastOne then
            manyCollection1 df
        else
            manyCollection df
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TCollection}"/>
    /// that recognizes many occurrences of the given
    /// <see cref="DesigntimeFarkle{TResult}"/>, separated by a
    /// <see cref="DesigntimeFarkle"/>.</summary>
    /// <param name="atLeastOne">Whether at least one occurrence
    /// is required. Defaults to false.</param>
    /// <typeparam name="TCollection">The type of the collection to
    /// store the results. It must implement
    /// <see cref="System.Collections.Generic.ICollection{TResult}"/>
    /// and have a parameterless constructor.</typeparam>
    /// <seealso cref="Many"/>
    static member SeparatedBy<'TResult, 'TCollection
        when 'TCollection :> ICollection<'TResult>
            and 'TCollection: (new: unit -> 'TCollection)> (df: DesigntimeFarkle<'TResult>, separator: DesigntimeFarkle,
            [<Optional; DefaultParameterValue(false)>] atLeastOne): DesigntimeFarkle<'TCollection> =
        let fName modifier = sprintf "%s%s %s" df.Name modifier typeof<'TCollection>.Name

        if atLeastOne then
            fName " Non-empty"
            ||= [!@ df.SeparatedBy(separator, false) .>> separator .>>. df => (fun xs x -> xs.Add(x); xs)]
        else
            let nont = nonterminal <| fName ""
            nont.SetProductions(
                empty => (fun () -> new 'TCollection()),
                !@ nont .>> separator .>>. df => (fun xs x -> (xs :> ICollection<_>) .Add(x); xs)
            )
            nont :> DesigntimeFarkle<_>
    [<Extension>]
    /// <summary>Creates a new <see cref="DesigntimeFarkle{TResult}"/>
    /// that might recognize the given one, or not. In the latter
    /// case, it returns the default value of the result type (null,
    /// zero, you got the idea).</summary>
    /// <seealso cref="Nullable"/>
    static member Optional(df: DesigntimeFarkle<'TResult>) =
        Nonterminal.Create(sprintf "%s Maybe" df.Name,
            df.Extended().AsIs(),
            ProductionBuilder.Empty.FinishConstant(Unchecked.defaultof<'TResult>))
    [<Extension>]
    /// <seealso cref="Optional"/>
    static member Nullable(df: DesigntimeFarkle<'TResult>) =
        Nonterminal.Create(sprintf "%s Maybe" df.Name,
            df.Extended().Finish(Nullable.op_Implicit),
            ProductionBuilder.Empty.FinishConstant(Unchecked.defaultof<'TResult Nullable>))
