// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools.Precompiler

open System.IO
open Microsoft.Build.Framework
open System
open System.Text.Encodings.Web
open System.Text.Json

// The JSON serializer needs this attribute.
[<CLIMutable>]
/// The input to the precompiler worker process.
type PrecompilerWorkerInput = {
    TaskLineNumber: int
    TaskColumnNumber: int
    TaskProjectFile: string
    References: string[]
    AssemblyPath: string
    SkipConflictReport: bool
}

/// Maps to MSBuild's error, warning and message types.
type LogEventSeverity =
    | Error = 0
    | Warning = 1
    | MessageHigh = 2
    | MessageNormal = 3
    | MessageLow = 4

/// An easily serializable representation of an MSBuild log event.
[<CLIMutable>]
type LogEvent = {
    Severity: LogEventSeverity
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
        | LogEventSeverity.Error ->
            let eventArgs = BuildErrorEventArgs(x.Subcategory, x.Code, x.File, x.LineNumber, x.ColumnNumber, x.EndLineNumber, x.EndColumnNumber, x.Message, x.HelpKeyword, x.SenderName, x.EventTimestamp)
            engine.LogErrorEvent eventArgs
        | LogEventSeverity.Warning ->
            let eventArgs = BuildWarningEventArgs(x.Subcategory, x.Code, x.File, x.LineNumber, x.ColumnNumber, x.EndLineNumber, x.EndColumnNumber, x.Message, x.HelpKeyword, x.SenderName, x.EventTimestamp)
            engine.LogWarningEvent eventArgs
        | _ ->
            let importance =
                match x.Severity with
                | LogEventSeverity.MessageHigh -> MessageImportance.High
                | LogEventSeverity.MessageNormal -> MessageImportance.Normal
                | _ -> MessageImportance.Low
            let eventArgs = BuildMessageEventArgs(x.Subcategory, x.Code, x.File, x.LineNumber, x.ColumnNumber, x.EndLineNumber, x.EndColumnNumber, x.Message, x.HelpKeyword, x.SenderName, importance, x.EventTimestamp)
            engine.LogMessageEvent eventArgs

[<CLIMutable>]
/// The output of the precompiler worker process.
type PrecompilerWorkerOutput = {
    Success: bool
    Messages: LogEvent []
}

module PrecompilerCommon =

    /// To be incremented when the IPC protocol changes.
    let ipcProtocolVersion = "1.0"

    /// The name of the precompiler weaver, for the purposes of Sigourney.
    let weaverName = "Farkle.Tools.Precompiler"

    let private defaultSerializerOptions =
        let x = JsonSerializerOptions()
        // This is not a web app, the JSON files will be used for IPC and not be
        // rendered in HTML. This relaxed escaping makes the files smaller and more readable.
        x.Encoder <- JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        x

    [<RequiresExplicitTypeArguments>]
    let readFromJsonFile<'a> path =
        let utf8Bytes = File.ReadAllBytes path
        JsonSerializer.Deserialize<'a>(ReadOnlySpan utf8Bytes)

    let writeToJsonFile path x =
        use stream = File.OpenWrite path
        use jw = new Utf8JsonWriter(stream)
        JsonSerializer.Serialize(jw, x, defaultSerializerOptions)
