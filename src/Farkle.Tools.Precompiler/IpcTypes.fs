// Copyright (c) 2021 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

// Contains types to be serialized and transmitted between
// the .NET Framework task and the precompiler worker.
namespace Farkle.Tools.Precompiler.IpcTypes

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

type LogMessage = {
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
    Messages: LogMessage []
}
