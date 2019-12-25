// Copyright (c) 2019 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.CSharp

open Farkle
open Farkle.Builder
open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AbstractClass; Sealed>]
/// A helper static class to create nonterminals.
type Nonterminal =
    static member Create(name) = nonterminal name

    static member Create(name, firstProduction, [<ParamArray>] productions) =
        name ||= (firstProduction :: List.ofArray productions)

[<Extension; AbstractClass; Sealed>]
/// <summary>Extension methods to create production builders.</summary>
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
