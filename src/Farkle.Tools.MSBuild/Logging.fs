// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tools.MSBuild

open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open System
open System.Resources

type Logging =
    static let resourceManager = ResourceManager("Farkle.Tools.MSBuild.Resources", typeof<Logging>.Assembly)

    static let getResourceString key = resourceManager.GetString key

    static let getHelpLink code = sprintf "https://farkle.dev/diagnostics/%s.html" code

    static member private LogErrorLocalized(log: TaskLoggingHelper, code, resourceKey, [<ParamArray>] args) =
        log.LogError(null, code, null, getHelpLink code, null, 0, 0, 0, 0, message=getResourceString resourceKey, messageArgs=args)

    static member private LogWarningLocalized(log: TaskLoggingHelper, code, resourceKey, [<ParamArray>] args) =
        log.LogWarning(null, code, null, getHelpLink code, null, 0, 0, 0, 0, message=getResourceString resourceKey, messageArgs=args)

    static member private LogMesssageLocalized(log: TaskLoggingHelper, importance, resourceKey, [<ParamArray>] args) =
        log.LogMessage(importance, message=getResourceString resourceKey, messageArgs=args)

    static member UnsupportedVS log =
        Logging.LogErrorLocalized(log, "FARKLE0018", "Precompiler_UnsupportedVS")

    static member UnrecognizedErrorMode log =
        Logging.LogWarningLocalized(log, "FARKLE0019", "Precompiler_UnrecognizedErrorMode")

    static member ConflictReport log (file: string) =
        Logging.LogMesssageLocalized(log, MessageImportance.High, "Precompiler_ConflictReport", file)

    static member ConflictReportAdvice log =
        Logging.LogMesssageLocalized(log, MessageImportance.High, "Precompiler_ConflictReportAdvice")

    static member WritingHtml log (grammarName: string) (file: string) =
        Logging.LogMesssageLocalized(log, MessageImportance.High, "Precompiler_WritingHtml", grammarName, file)
