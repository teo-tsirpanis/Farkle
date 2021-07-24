// Copyright (c) 2021 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Tools

open Farkle.Tools.Precompiler
open Microsoft.Build.Framework
open System

/// An IBuildEngine implementation that just stores log events in memory.
type LogSinkBuildMachine(taskLineNumber, taskColumnNumber, taskProjectFile) =
    let events = ResizeArray()
    let addEvent severity subcategory code file lineNumber columnNumber
        endLineNumber endColumnNumber message helpKeyword senderName eventTimestamp =
        lock events <| fun () ->
            events.Add {
                Severity = severity
                Subcategory = subcategory
                Code = code
                File = file
                LineNumber = lineNumber
                ColumnNumber = columnNumber
                EndLineNumber = endLineNumber
                EndColumnNumber = endColumnNumber
                Message = message
                HelpKeyword = helpKeyword
                SenderName = senderName
                EventTimestamp = eventTimestamp
            }
    member _.GetEventsToArray() = events.ToArray()
    interface IBuildEngine with
        member _.LogErrorEvent ev =
            addEvent LogEventSeverity.Error ev.Subcategory ev.Code ev.File ev.LineNumber ev.ColumnNumber
                ev.EndLineNumber ev.EndColumnNumber ev.Message ev.HelpKeyword ev.SenderName ev.Timestamp
        member _.LogWarningEvent ev =
            addEvent LogEventSeverity.Warning ev.Subcategory ev.Code ev.File ev.LineNumber ev.ColumnNumber
                ev.EndLineNumber ev.EndColumnNumber ev.Message ev.HelpKeyword ev.SenderName ev.Timestamp
        member _.LogMessageEvent ev =
            let severity =
                match ev.Importance with
                | MessageImportance.High -> LogEventSeverity.MessageHigh
                | MessageImportance.Normal -> LogEventSeverity.MessageNormal
                | _ -> LogEventSeverity.MessageLow
            addEvent severity ev.Subcategory ev.Code ev.File ev.LineNumber ev.ColumnNumber
                ev.EndLineNumber ev.EndColumnNumber ev.Message ev.HelpKeyword ev.SenderName ev.Timestamp
        member _.LogCustomEvent _ =
            NotSupportedException "Logging custom events is not supported."
            |> raise
        member _.ContinueOnError = false
        member _.LineNumberOfTaskNode = taskLineNumber
        member _.ColumnNumberOfTaskNode = taskColumnNumber
        member _.ProjectFileOfTaskNode = taskProjectFile
        member _.BuildProjectFile(_, _, _, _) = false
