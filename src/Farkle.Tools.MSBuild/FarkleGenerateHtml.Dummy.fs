// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tools.MSBuild

open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open System

type FarkleGenerateHtml() =
    inherit Task()

    [<Required>]
    member val AssemblyPath = "" with get, set

    [<Required>]
    member val OutputDirectory = "" with get, set

    [<Output>]
    member val GeneratedFiles = Array.Empty<ITaskItem>() with get, set

    override this.Execute() =
        // TODO: Localize message
        this.Log.LogError("Farkle's precompiler is not compatible with Visual Studio 2022 or earlier.")
        false
