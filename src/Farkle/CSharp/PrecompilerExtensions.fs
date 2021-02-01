// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle
open Farkle.Builder
open System
open System.Runtime.CompilerServices

[<Extension>]
/// Extension methods that create and build precompilable designtime Farkles.
type PrecompilerExtensions =
    [<Extension; MethodImpl(MethodImplOptions.NoInlining)>]
    /// <summary>Marks a <see cref="DesigntimeFarkle{TResult}"/> as available
    /// to have its grammar precompiled ahead of time.</summary>
    /// <remarks>This function has to be directly called from
    /// user code. Learn more, including usage restrictions at
    /// <a href="https://teo-tsirpanis.github.io/Farkle/the-precompiler.html"/></remarks>
    /// <seealso cref="Farkle.RuntimeFarkle.markForPrecompile"/>
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
    /// user code. Learn more, including usage restrictions at
    /// <a href="https://teo-tsirpanis.github.io/Farkle/the-precompiler.html"/></remarks>
    /// <seealso cref="Farkle.RuntimeFarkle.markForPrecompileU"/>
    static member MarkForPrecompile df =
        // This function must mot forward to RuntimeFarkle's
        // corresponding function. It would register it
        // with Farkle's own assembly otherwise.
        let asm = Reflection.Assembly.GetCallingAssembly()
        PrecompilableDesigntimeFarkle(df, asm)
    [<Extension>]
    /// <summary>Builds a <see cref="PrecompilableDesigntimeFarkle{TResult}"/> into
    /// a <see cref="RuntimeFarkle{TResult}"/>.</summary>
    /// <remarks>If the designtime Farkle is not precompiled the resulting
    /// runtime Farkle will fail every time it is used.</remarks>
    static member Build (df: PrecompilableDesigntimeFarkle<'TResult>) =
        RuntimeFarkle.buildPrecompiled df
    [<Extension>]
    static member BuildUntyped df =
        df
        |> PrecompilerInterface.buildPrecompiled
        |> RuntimeFarkle<_>.CreateMaybe RuntimeFarkle.syntaxCheckerObj
