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
/// <remarks>Most of these methods must be applied to the topmost designtime Farkle
/// that will eventually be built. Use <see cref="Cast"/> first to use these functions
/// on untyped designtime Farkles.</remarks>
type DesigntimeFarkleExtensions =
    [<Extension>]
    /// <summary>Casts a <see cref="DesigntimeFarkle"/>
    /// into a <see cref="DesigntimeFarkle{Object}"/></summary>
    /// <remarks>Useful for setting metadata to untyped designtime Farkles.
    /// The object <paramref name="df"/> will return is undefined.</remarks>
    static member Cast df: [<Nullable(1uy, 2uy)>] _ = DesigntimeFarkle.cast df
    [<Extension>]
    /// <summary>Changes the name of a <see cref="DesigntimeFarkle{TResult}"/>.</summary>
    /// <remarks>Useful for diagnostic purposes.</remarks>
    static member Rename (df: DesigntimeFarkle<'TResult>, name) = DesigntimeFarkle.rename name df
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle{TResult}"/>
    /// into a <see cref="RuntimeFarkle{TResult}"/>.</summary>
    static member Build (df: DesigntimeFarkle<'TResult>) = RuntimeFarkle.build df
    [<Extension>]
    /// <summary>Builds a <see cref="DesigntimeFarkle"/> into a syntax-checking
    /// <see cref="RuntimeFarkle{System.Object}"/>.</summary>
    static member BuildUntyped df = RuntimeFarkle.buildUntyped(df).SyntaxCheck()
    [<Extension; MethodImpl(MethodImplOptions.NoInlining)>]
    /// <summary>Marks a <see cref="DesigntimeFarkle{TResult}"/> as available to have
    /// its grammar precompiled ahead of time.</summary>
    /// <remarks>This function has to be directly called from
    /// user code. See more, including usage restrictions at
    /// <a href="https://teo-tsirpanis.github.io/Farkle/the-precompiler.html"/></remarks>
    /// <seealso cref="Farkle.RuntimeFarkle.MarkForPrecompile"/>
    static member MarkForPrecompile (df: DesigntimeFarkle<'TResult>) =
        // This function must mot forward to RuntimeFarkle's
        // corresponding function. It would register it
        // with Farkle's own assembly otherwise.
        let asm = Reflection.Assembly.GetCallingAssembly()
        PrecompilableDesigntimeFarkle<_>(df, asm)
    [<Extension; MethodImpl(MethodImplOptions.NoInlining)>]
    /// <summary>Marks an untyped <see cref="DesigntimeFarkle{TResult}"/>
    /// as available to have its grammar precompiled ahead of time.</summary>
    /// <remarks>This function has to be directly called from
    /// user code. See more, including usage restrictions at
    /// <a href="https://teo-tsirpanis.github.io/Farkle/the-precompiler.html"/></remarks>
    /// <seealso cref="Farkle.RuntimeFarkle.MarkForPrecompileU"/>
    static member MarkForPrecompile (df: DesigntimeFarkle) =
        // This function must mot forward to RuntimeFarkle's
        // corresponding function. It would register it
        // with Farkle's own assembly otherwise.
        let asm = Reflection.Assembly.GetCallingAssembly()
        PrecompilableDesigntimeFarkle(df, asm)
    [<Extension>]
    /// <summary>Sets a custom <see cref="GrammarMetadata"/>
    /// object to a typed designtime Farkle.</summary>
    /// <seealso cref="CaseSensitive"/>
    /// <seealso cref="AutoWhitespace"/>
    /// <seealso cref="AddNoiseSymbol"/>
    /// <seealso cref="AddLineComment"/>
    /// <seealso cref="AddBlockComment"/>
    static member WithMetadata (df: DesigntimeFarkle<'TResult>, metadata) =
        DesigntimeFarkle.withMetadata metadata df
    [<Extension>]
    /// <summary>Sets an <see cref="Farkle.Builder.OperatorPrecedence.OperatorScope"/>
    /// to a typed designtime Farkle. Any previous ones -if exist- are discarded.</summary>
    static member WithOperatorScope (df: DesigntimeFarkle<'TResult>, opScope) =
        DesigntimeFarkle.withOperatorScope opScope df
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
    /// <param name="df">The designtime Farkle to recognize many times.</param>
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
