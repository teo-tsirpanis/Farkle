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
open Microsoft.Build.Utilities

/// What to do if the precompiler encounters an error with the grammar.
/// The "errors" this type controls are only LALR conflicts at the moment
/// but could be expanded in the future (not planned).
type ErrorMode =
    /// Create an HTML report and just mention it in MSBuild. This is the default.
    | ReportOnly = 0
    /// Display each error individually through MSBuild.
    | ErrorsOnly = 1
    // Do both.
    | Both = 2

/// The input to the precompiler worker process.
// The JSON serializer needs this attribute.
[<CLIMutable>]
type PrecompilerWorkerInput = {
    TaskLineNumber: int
    TaskColumnNumber: int
    TaskProjectFile: string
    References: string[]
    AssemblyPath: string
    ErrorMode: ErrorMode
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

/// The output of the precompiler worker process.
[<CLIMutable>]
type PrecompilerWorkerOutput = {
    Success: bool
    Messages: LogEvent []
    GeneratedConflictReports: string []
}

module PrecompilerCommon =

    /// To be incremented when the IPC protocol changes.
    // 1.0: Initial version
    // 1.1: Replaced SkipConflictReport with ErrorMode
    let ipcProtocolVersion = "1.1"

    /// The name of the precompiler weaver, for the purposes of Sigourney.
    let weaverName = "Farkle.Tools.Precompiler"

    /// An log message to be shown to hint how to dsable conflict reports.
    let conflictReportHint = "Instead of creating an HTML report, the individual LALR conflicts \
can be shown as errors by setting the 'FarklePrecompilerErrorMode' MSbuild property to 'Both' or 'ErrorsOnly'."

    let private tryParseErrorMode (x: string) =
        match Enum.TryParse<ErrorMode>(x, true) with
        | true, errorMode ->
#if NET
            if Enum.IsDefined errorMode then
#else
            if Enum.IsDefined(typeof<ErrorMode>, errorMode) then
#endif
                errorMode
                |> ValueSome
            else
                ValueNone
        | _ -> ValueNone

    let getErrorMode (log: TaskLoggingHelper) skipConflictReport errorMode =
        let fromSkipConflictReport =
            if skipConflictReport then ErrorMode.ErrorsOnly else ErrorMode.ReportOnly
        if String.IsNullOrWhiteSpace(errorMode) then
            fromSkipConflictReport
        else
            match tryParseErrorMode errorMode with
            | ValueSome x -> x
            | ValueNone ->
                log.LogWarning("Could not recognize the value of FarklePrecompilerErrorMode, defaulting to ReportOnly.")
                ErrorMode.ReportOnly

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
