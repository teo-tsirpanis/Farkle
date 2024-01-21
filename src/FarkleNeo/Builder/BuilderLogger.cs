// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Diagnostics;

namespace Farkle.Builder;

internal struct BuilderLogger
{
    public DiagnosticSeverity LogLevel { get; set; }

    public event Action<BuilderDiagnostic>? OnDiagnostic;

    public readonly bool IsEnabled(DiagnosticSeverity severity) => severity >= LogLevel;

    public readonly void Log(DiagnosticSeverity severity, object message, string? code = null)
    {
        Debug.Assert(severity >= DiagnosticSeverity.Verbose && severity <= DiagnosticSeverity.Error);
        Debug.Assert(!(severity >= DiagnosticSeverity.Warning && code is null), "Warnings and above must have a code.");
        Debug.Assert(!(severity >= DiagnosticSeverity.Information && message is not IFormattable), "Informational diagnostics and above must be localizable.");
        if (severity >= LogLevel)
        {
            OnDiagnostic?.Invoke(new BuilderDiagnostic(severity, message, code));
        }
    }
}
