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

    public static void DuplicateSpecialName(in this BuilderLogger logger, string specialName) =>
        logger.Error("FARKLE0004", LocalizedDiagnostic.Create(nameof(Resources.Builder_DuplicateSpecialName), specialName));

    public static void NonterminalProductionsNotSet(in this BuilderLogger logger, string nonterminalName) =>
        logger.Warning("FARKLE0005", LocalizedDiagnostic.Create(nameof(Resources.Builder_NonterminalProductionsNotSet), nonterminalName));

    public static void DuplicateOperatorSymbol(in this BuilderLogger logger, object symbol, int existingPrecedence, int newPrecedence) =>
        logger.Warning("FARKLE0006", LocalizedDiagnostic.Create(nameof(Resources.Builder_DuplicateOperatorSymbol), symbol, existingPrecedence, newPrecedence));

    public static void Debug(in this BuilderLogger logger, string message) => logger.Log(DiagnosticSeverity.Debug, message);

    public static void Verbose(in this BuilderLogger logger, string message) => logger.Log(DiagnosticSeverity.Verbose, message);
}
