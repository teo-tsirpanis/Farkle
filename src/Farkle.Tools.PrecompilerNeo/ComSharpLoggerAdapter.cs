// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using ComSharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Farkle.Tools.Precompiler;

internal sealed class ComSharpLoggerAdapter : ComSharp.ILogger
{
    private readonly TaskLoggingHelper _log;

    public DiagnosticSeverity LogLevel { get; }

    public ComSharpLoggerAdapter(TaskLoggingHelper log)
    {
        _log = log;
        if (log.LogsMessagesOfImportance(MessageImportance.Low))
            LogLevel = DiagnosticSeverity.Verbose;
        else if (log.LogsMessagesOfImportance(MessageImportance.Normal))
            LogLevel = DiagnosticSeverity.Debug;
        else if (log.LogsMessagesOfImportance(MessageImportance.High))
            LogLevel = DiagnosticSeverity.Information;
        else
            LogLevel = DiagnosticSeverity.Warning;
    }

    public void Log(DiagnosticSeverity severity, object message, string? code)
    {
        string? helpLink = code is not null ? string.Format(Obsoletions.SharedUrlFormat, code) : null;
        string? messageString = message.ToString();
        switch (severity)
        {
            case DiagnosticSeverity.Error:
                _log.LogError(subcategory: null, code, null, helpLink, null, 0, 0, 0, 0, messageString);
                return;
            case DiagnosticSeverity.Warning:
                _log.LogWarning(subcategory: null, code, null, helpLink, null, 0, 0, 0, 0, messageString);
                return;
        }
        MessageImportance importance = severity switch
        {
            DiagnosticSeverity.Information => MessageImportance.High,
            DiagnosticSeverity.Debug => MessageImportance.Normal,
            _ => MessageImportance.Low,
        };
        _log.LogMessage(subcategory: null, code, null, null, 0, 0, 0, 0, importance, messageString);
    }
}
