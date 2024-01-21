// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;

namespace Farkle.Builder;

/// <summary>
/// Provides options to configure the process of building a grammar.
/// </summary>
/// <remarks>
/// The options in this class do not affect the result of the build process.
/// </remarks>
public sealed class BuilderOptions
{
    /// <summary>
    /// Used to cancel the build process.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// The maximum number of states that the tokenizer can have.
    /// </summary>
    /// <remarks>
    /// This value can be used to prevent exponential blowup of the tokenizer
    /// states for certain regexes like <c>[ab]*[ab]{32}</c>. If it is zero
    /// or negative, the limit is set to an implementation-defined number
    /// that is proportional to the complexity of the input regexes.
    /// </remarks>
    public int MaxTokenizerStates { get; set; } = -1;

    internal BuilderLogger Log = new() { LogLevel = DiagnosticSeverity.Information };

    /// <summary>
    /// An event that is raised when a diagnostic is reported.
    /// </summary>
    /// <seealso cref="LogLevel"/>
    public event Action<BuilderDiagnostic>? OnDiagnostic
    {
        add => Log.OnDiagnostic += value;
        remove => Log.OnDiagnostic -= value;
    }

    /// <summary>
    /// The minimum severity of diagnostics that are reported.
    /// Defaults to <see cref="DiagnosticSeverity.Information"/>.
    /// </summary>
    /// <seealso cref="OnDiagnostic"/>
    /// <seealso cref="BuilderDiagnostic.Severity"/>
    public DiagnosticSeverity LogLevel
    {
        get => Log.LogLevel;
        set => Log.LogLevel = value;
    }

    internal static int GetMaxTokenizerStates(int maxTokenizerStates, int numLeaves)
    {
        if (maxTokenizerStates > 0)
        {
            return maxTokenizerStates;
        }

        long limit = (long)numLeaves * 16;
        if (limit > int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Max(256, (int)limit);
    }
}
