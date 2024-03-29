// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics.Builder;

internal static class BuilderLoggerExtensions
{
    private static void Error(in this BuilderLogger logger, string code, object message) =>
        logger.Log(DiagnosticSeverity.Error, message, code);

    private static void Warning(in this BuilderLogger logger, string code, object message) =>
        logger.Log(DiagnosticSeverity.Warning, message, code);

    public static void DfaStateLimitExceeded(in this BuilderLogger logger, int maxStates) =>
        logger.Error("FARKLE0001", LocalizedDiagnostic.Create(nameof(Resources.Builder_DfaStateLimitExceeded), maxStates));

    public static void IndistinguishableSymbols(in this BuilderLogger logger, IndistinguishableSymbolsError error) =>
        logger.Error("FARKLE0002", error);

    public static void RegexContainsVoid(in this BuilderLogger logger, in BuilderSymbolName symbolName) =>
        logger.Warning("FARKLE0003", LocalizedDiagnostic.Create(nameof(Resources.Builder_RegexContainsVoid), symbolName));

    public static void Debug(in this BuilderLogger logger, string message) => logger.Log(DiagnosticSeverity.Debug, message);

    public static void Verbose(in this BuilderLogger logger, string message) => logger.Log(DiagnosticSeverity.Verbose, message);
}
