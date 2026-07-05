// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tools.MSBuild

open Microsoft.Build.Framework
open Sigourney
open System

[<MSBuildMultiThreadableTask>]
type FarklePrecompilerTask() =
    // Inherit from MSBuildWeaver to get identical task properties.
    inherit MSBuildWeaver()

    [<Required>]
    member val RuntimeDependencies: ITaskItem[] = Array.Empty() with get, set

    member val SkipConflictReport = false with get, set

    member val ErrorMode = "" with get, set

    [<Output>]
    member val GeneratedConflictReports = Array.Empty<ITaskItem>() with get, set

    override this.Execute() =
        Logging.UnsupportedVS this.Log
        false
    override _.DoWeave _ = false
