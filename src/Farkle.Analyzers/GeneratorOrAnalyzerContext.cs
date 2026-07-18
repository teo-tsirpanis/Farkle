// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace Farkle.Analyzers;

/// <summary>
/// Provides information of interest to either a source generator or an analyzer.
/// </summary>
public readonly struct GeneratorOrAnalyzerContext<T>
{
    /// <summary>
    /// Adds an item to an incremental source generator pipeline. This will be null when the context is used in an analyzer.
    /// </summary>
    public Action<T>? AddItem { get; }

    /// <summary>
    /// Reports a diagnostic. This will be null when the context is used in a source generator.
    /// </summary>
    public Action<Diagnostic>? ReportDiagnostic { get; }

    /// <summary>
    /// The semantic model of the syntax tree being analyzed.
    /// </summary>
    public SemanticModel SemanticModel { get; }

    public GeneratorOrAnalyzerContext(Action<T> addItem, SemanticModel semanticModel)
    {
        AddItem = addItem;
        SemanticModel = semanticModel;
    }

    public GeneratorOrAnalyzerContext(Action<Diagnostic> reportDiagnostic, SemanticModel semanticModel)
    {
        ReportDiagnostic = reportDiagnostic;
        SemanticModel = semanticModel;
    }
}
