// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tools.Precompiler;

/// <summary>
/// Specifies how the precompiler should report conflict errors in the grammar.
/// </summary>
public enum ConflictReportMode
{
    /// <summary>
    /// Create an HTML report and just mention it in MSBuild. This is the default.
    /// </summary>
    ReportOnly,
    /// <summary>
    /// Report each conflict individually through MSBuild.
    /// </summary>
    ErrorsOnly,
    /// <summary>
    /// Combine both <see cref="ReportOnly"/> and <see cref="ErrorsOnly"/>.
    /// </summary>
    Both,
}
