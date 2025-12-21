// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using ComSharp;

namespace Farkle.Tools.Precompiler;

internal sealed class ComSharpPrecompilerOptions(ILogger? log, CancellationToken ct) : IPrecompilerOptions
{
    public CancellationToken CancellationToken { get; } = ct;

    public ILogger? Logger { get; } = log;
}
