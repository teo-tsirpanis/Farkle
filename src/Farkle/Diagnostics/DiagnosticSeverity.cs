// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics.Builder;

namespace Farkle.Diagnostics;

/// <summary>
/// Specifies the severity of a diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// A very verbose diagnostic useful for debugging Farkle.
    /// </summary>
    Verbose,
    /// <summary>
    /// A verbose diagnostic useful for debugging the application that uses Farkle.
    /// </summary>
    Debug,
    /// <summary>
    /// An diagnostic that provides an informational message.
    /// </summary>
    /// <remarks>
    /// Diagnostics produced by Farkle's builder with this severity are
    /// localizable.
    /// </remarks>
    Information,
    /// <summary>
    /// A diagnostic that indicates a potentially undesirable behavior.
    /// </summary>
    /// <remarks>
    /// Diagnostics produced by Farkle's builder with this severity are
    /// localizable and have a <see cref="BuilderDiagnostic.Code"/>.
    /// </remarks>
    Warning,
    /// <summary>
    /// An diagnostic that indicates a failed operation.
    /// </summary>
    /// <remarks>
    /// Diagnostics produced by Farkle's builder with this severity are
    /// localizable and have a <see cref="BuilderDiagnostic.Code"/>.
    /// </remarks>
    Error
}
