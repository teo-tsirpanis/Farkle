// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics.Builder;

/// <summary>
/// Contains information about actions performed by Farkle's builder.
/// </summary>
public readonly struct BuilderDiagnostic : IFormattable
{
    /// <summary>
    /// The severity of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// An <see cref="object"/> that describes the message.
    /// </summary>
    public object Message { get; }

    /// <summary>
    /// The code of the diagnostic.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Creates a <see cref="BuilderDiagnostic"/>.
    /// </summary>
    /// <param name="severity">The value of <see cref="Severity"/>.</param>
    /// <param name="message">The value of <see cref="Message"/>.</param>
    /// <param name="code">The value of <see cref="Code"/>. Optional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/>
    /// is <see langword="null"/>.</exception>
    public BuilderDiagnostic(DiagnosticSeverity severity, object message, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        Severity = severity;
        Message = message;
        Code = code;
    }

    private string ToString(IFormatProvider? formatProvider)
    {
        string severity = Severity switch
        {
            DiagnosticSeverity.Warning => Resources.Warning,
            DiagnosticSeverity.Error => Resources.Error,
            _ => ""
        };
        string severityPadding = severity.Length > 0 ? " " : "";
        string code = Code is null ? "" : Code;
        string codePadding = code.Length > 0 ? " " : "";
        return string.Create(formatProvider, $"{severity}{severityPadding}{code}{codePadding}{Message}");
    }

    /// <inheritdoc/>
    public override string ToString() => ToString(null);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);
}
