// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Diagnostics.Builder;

internal struct BuilderLogger
{
    public DiagnosticSeverity LogLevel { get; set; }

    public event Action<BuilderDiagnostic>? OnDiagnostic;

    public readonly bool IsEnabled(DiagnosticSeverity severity) => OnDiagnostic is not null && severity >= LogLevel;

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

    /// <summary>
    /// Creates a new <see cref="BuilderLogger"/> with an additional event listener that adds
    /// all diagnostics of severity <see cref="DiagnosticSeverity.Error"/> to the specified
    /// collection.
    /// </summary>
    /// <param name="errors">The collection to add each error diagnostic into.
    /// If it is <see langword="null"/>, it will be ignored.</param>
    public readonly BuilderLogger WithRedirectErrors(ICollection<BuilderDiagnostic>? errors)
    {
        BuilderLogger log = this;
        if (errors is not null)
        {
            log.OnDiagnostic += diagnostic =>
            {
                if (diagnostic.Severity >= DiagnosticSeverity.Error)
                {
                    errors.Add(diagnostic);
                }
            };
        }
        return log;
    }
}
