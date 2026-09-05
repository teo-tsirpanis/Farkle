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

    private static LocalizableResourceString Localize(string resourceName) => new(resourceName, Resources.ResourceManager, typeof(Resources));

    private const string CategoryUsage = "Usage";

    private const string CategoryMaintainability = "Maintainability";

    public static readonly DiagnosticDescriptor ProductionBuilderFactoryRequiresEnhancedSyntax = Create(
        id: "FARKLE1005",
        title: Localize(nameof(Resources.ProductionBuilderFactoryRequiresEnhancedSyntax_Title)),
        messageFormat: Localize(nameof(Resources.ProductionBuilderFactoryRequiresEnhancedSyntax_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.ProductionBuilderFactoryRequiresEnhancedSyntax_Description)),
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor ProductionBuilderFactoryUnsupportedType = Create(
        id: "FARKLE1006",
        title: Localize(nameof(Resources.FARKLE1006_Common_Title)),
        messageFormat: Localize(nameof(Resources.ProductionBuilderFactoryUnsupportedType_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.ProductionBuilderFactoryUnsupportedType_Description)),
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor ProductionBuilderFactoryTooManyTypedGrammarSymbols = Create(
        id: "FARKLE1006",
        title: Localize(nameof(Resources.FARKLE1006_Common_Title)),
        messageFormat: Localize(nameof(Resources.ProductionBuilderFactoryTooManyTypedSymbols_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor UseEnhancedSyntaxAttributeUnnecessary = Create(
        id: "FARKLE1007",
        title: Localize(nameof(Resources.UseEnhancedSyntaxAttributeUnnecessary_Title)),
        messageFormat: Localize(nameof(Resources.UseEnhancedSyntaxAttributeUnnecessary_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.UseEnhancedSyntaxAttributeUnnecessary_Description)),
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor SwitchToProductionFactories = Create(
        id: "FARKLE1008",
        title: Localize(nameof(Resources.SwitchToProductionFactories_Title)),
        messageFormat: Localize(nameof(Resources.SwitchToProductionFactories_MessageFormat)),
        category: CategoryMaintainability,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.SwitchToProductionFactories_Description)),
        customTags: [WellKnownDiagnosticTags.Unnecessary]);

    public static readonly DiagnosticDescriptor CannotInferProductionBuilderFactoryParameters = Create(
        id: "FARKLE1009",
        title: Localize(nameof(Resources.CannotInferProductionBuilderFactoryParameters_Title)),
        messageFormat: Localize(nameof(Resources.CannotInferProductionBuilderFactoryParameters_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.CannotInferProductionBuilderFactoryParameters_Description)));

    public static readonly SuppressionDescriptor IGrammarSymbolUpcastSuppressor = new(
        id: "FARKLE2001",
        suppressedDiagnosticId: "IDE0004",
        justification: Localize(nameof(Resources.IGrammarSymbolUpcastSuppressor_Justification)));

    public static readonly SuppressionDescriptor PrecompilerInputMethodUnusedSuppressor = new(
        id: "FARKLE2002",
        suppressedDiagnosticId: "IDE0051",
        justification: Localize(nameof(Resources.PrecompilerInputMethodUnusedSuppressor_Justification)));
}
