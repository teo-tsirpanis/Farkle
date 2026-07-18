// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace Farkle.Analyzers;

public static class DiagnosticDescriptors
{
    private static DiagnosticDescriptor Create(
        string id,
        LocalizableString title,
        LocalizableString messageFormat,
        string category,
        DiagnosticSeverity defaultSeverity,
        bool isEnabledByDefault,
        LocalizableString? description = null,
        params string[] customTags) => new(
            id, title, messageFormat, category, defaultSeverity, isEnabledByDefault, description,
            helpLinkUri: $"https://farkle.dev/diagnostics/{id}.html", customTags);

    public static readonly DiagnosticDescriptor ProductionFactoryRequiresEnhancedSyntax = Create(
        id: "FARKLE1005",
        title: "API requires applying 'Farkle.Builder.UseEnhancedSyntaxAttribute'",
        messageFormat: "Using class 'Farkle.Builder.Production' requires applying 'Farkle.Builder.UseEnhancedSyntaxAttribute'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "To ensure high performance for the production factory source generator, using a method in the 'Farkle.Builder.Production' class requires applying 'UseEnhancedSyntaxAttribute'. The attribute can be applied to either the member using the production factory, or to its containing type.",
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor ProductionFactoryUnsupportedType = Create(
        id: "FARKLE1006",
        title: "Invalid usage of Farkle.Builder.Production factory method",
        messageFormat: "Argument {0}: cannot convert from '{1}' to 'string' or 'Farkle.Builder.IGrammarSymbol'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Production factory methods only support arguments of type 'string' or 'Farkle.Builder.IGrammarSymbol'.",
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor ProductionFactoryTooManyTypedGrammarSymbols = Create(
        id: "FARKLE1006",
        title: "Invalid usage of Farkle.Builder.Production factory method",
        messageFormat: "Production factory method cannot contain more than {0} arguments of type 'Farkle.Builder.IGrammarSymbol<T>'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);
}
