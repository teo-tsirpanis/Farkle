// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using ComSharp;

namespace Farkle.Tools.Precompiler;

internal sealed class LrConflictTracker(ILogger log, bool emitConflictErrors) : ILogger
{
    private const string LrConflictErrorCode = "FARKLE0007";

    public DiagnosticSeverity LogLevel => log.LogLevel;

    public int ConflictCount { get; private set; }

    public void Reset() => ConflictCount = 0;

    void ILogger.Log(DiagnosticSeverity severity, object message, string? code)
    {
        if (severity == DiagnosticSeverity.Error && code is not null && code == LrConflictErrorCode)
        {
            ConflictCount++;
            if (!emitConflictErrors)
            {
                return;
            }
        }
        log.Log(severity, message, code);
    }
}
