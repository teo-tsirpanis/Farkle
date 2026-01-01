// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Microsoft.Build.Utilities;

namespace Farkle.Tools.Precompiler;

public sealed class PrecompilerOptions
{
    public CancellationToken CancellationToken { get; set; }

    public TaskLoggingHelper? Logger { get; set; }

    public IReadOnlyCollection<string> RuntimeDependencies { get; set; } = [];

    public ConflictReportMode ConflictReportMode { get; set; }

    public event Action<ImmutableArray<byte>>? OnGrammarConflict;

    internal void GrammarConflict(ImmutableArray<byte> grammarFile) => OnGrammarConflict?.Invoke(grammarFile);
}
