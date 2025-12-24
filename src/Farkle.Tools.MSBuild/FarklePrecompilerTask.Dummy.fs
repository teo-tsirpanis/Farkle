// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tools.MSBuild

open Microsoft.Build.Framework
open Sigourney
open System

type FarklePrecompilerTask() =
    inherit MSBuildWeaver()

    member val SkipConflictReport = false with get, set

    member val ErrorMode = "" with get, set

    [<Output>]
    member val GeneratedConflictReports = Array.Empty<ITaskItem>() with get, set

    override this.Execute() =
        // TODO: Localize message
        this.Log.LogError("Farkle's precompiler is not compatible with Visual Studio 2022 or earlier.")
        false
    override _.DoWeave _ = false
