// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;

namespace Farkle.Builder;

internal static class BuilderLoggerExtensions
{
    public static void Debug(in this BuilderLogger logger, string message) => logger.Log(DiagnosticSeverity.Debug, message);

    public static void Verbose(in this BuilderLogger logger, string message) => logger.Log(DiagnosticSeverity.Verbose, message);
}
