// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// Contains types to be serialized and transmitted between the .NET
// Framework MSBuild precompiler task and the .NET Core worker process.
namespace Farkle.Tools.PrecompilerIpcTypes

open Microsoft.Build.Framework
open System

type Input = {
    References: string[]
    AssemblyPath: string
}

type MessageSeverity =
    | Error = 0
    | Warning = 1
    | MessageHigh = 2
    | MessageNormal = 3
    | MessageLow = 4

/// An easily serializable representation of an MSBuild log event.
type LogEvent = {
    Severity: MessageSeverity
    Subcategory: string
    Code: string
    File: string
    LineNumber: int
    ColumnNumber: int
    EndLineNumber: int
    EndColumnNumber: int
    Message: string
    HelpKeyword: string
    SenderName: string
    EventTimestamp: DateTime
}
with
    /// Logs this event to an MSBuild build engine.
    member x.LogTo(engine: IBuildEngine) =
        match x.Severity with
        | MessageSeverity.Error ->
            let eventArgs = BuildErrorEventArgs(x.Subcategory, x.Code, x.File, x.LineNumber, x.ColumnNumber, x.EndLineNumber, x.EndColumnNumber, x.Message, x.HelpKeyword, x.SenderName, x.EventTimestamp)
            engine.LogErrorEvent eventArgs
        | MessageSeverity.Warning ->
            let eventArgs = BuildWarningEventArgs(x.Subcategory, x.Code, x.File, x.LineNumber, x.ColumnNumber, x.EndLineNumber, x.EndColumnNumber, x.Message, x.HelpKeyword, x.SenderName, x.EventTimestamp)
            engine.LogWarningEvent eventArgs
        | _ ->
            let importance =
                match x.Severity with
                | MessageSeverity.MessageHigh -> MessageImportance.High
                | MessageSeverity.MessageNormal -> MessageImportance.Normal
                | _ -> MessageImportance.Low
            let eventArgs = BuildMessageEventArgs(x.Subcategory, x.Code, x.File, x.LineNumber, x.ColumnNumber, x.EndLineNumber, x.EndColumnNumber, x.Message, x.HelpKeyword, x.SenderName, importance, x.EventTimestamp)
            engine.LogMessageEvent eventArgs

type Output = {
    Success: bool
    Messages: LogEvent []
}
