namespace Farkle.Tools

open Farkle.Tools.PrecompilerIpcTypes
open Microsoft.Build.Framework
open System

/// An IBuildEngine implementation that just stores log events in memory.
type LogSinkBuildMachine() =
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
            addEvent MessageSeverity.Error ev.Subcategory ev.Code ev.File ev.LineNumber ev.ColumnNumber
                ev.EndLineNumber ev.EndColumnNumber ev.Message ev.HelpKeyword ev.SenderName ev.Timestamp
        member _.LogWarningEvent ev =
            addEvent MessageSeverity.Warning ev.Subcategory ev.Code ev.File ev.LineNumber ev.ColumnNumber
                ev.EndLineNumber ev.EndColumnNumber ev.Message ev.HelpKeyword ev.SenderName ev.Timestamp
        member _.LogMessageEvent ev =
            let severity =
                match ev.Importance with
                | MessageImportance.High -> MessageSeverity.MessageHigh
                | MessageImportance.Normal -> MessageSeverity.MessageNormal
                | _ -> MessageSeverity.MessageLow
            addEvent severity ev.Subcategory ev.Code ev.File ev.LineNumber ev.ColumnNumber
                ev.EndLineNumber ev.EndColumnNumber ev.Message ev.HelpKeyword ev.SenderName ev.Timestamp
        member _.LogCustomEvent _ =
            NotSupportedException "Logging custom events is not supported."
            |> raise
        member _.ContinueOnError = false
        member _.LineNumberOfTaskNode = 0
        member _.ColumnNumberOfTaskNode = 0
        member _.ProjectFileOfTaskNode = "<undefined>"
        member _.BuildProjectFile(_, _, _, _) = false
