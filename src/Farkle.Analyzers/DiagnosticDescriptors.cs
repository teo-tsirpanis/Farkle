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

    public static readonly DiagnosticDescriptor ProductionFactoryRequiresEnhancedSyntax = Create(
        id: "FARKLE1005",
        title: Localize(nameof(Resources.ProductionFactoryRequiresEnhancedSyntax_Title)),
        messageFormat: Localize(nameof(Resources.ProductionFactoryRequiresEnhancedSyntax_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.ProductionFactoryRequiresEnhancedSyntax_Description)),
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor ProductionFactoryUnsupportedType = Create(
        id: "FARKLE1006",
        title: Localize(nameof(Resources.FARKLE1006_Common_Title)),
        messageFormat: Localize(nameof(Resources.ProductionFactoryUnsupportedType_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.ProductionFactoryUnsupportedType_Description)),
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public static readonly DiagnosticDescriptor ProductionFactoryTooManyTypedGrammarSymbols = Create(
        id: "FARKLE1006",
        title: Localize(nameof(Resources.FARKLE1006_Common_Title)),
        messageFormat: Localize(nameof(Resources.ProductionFactoryTooManyTypedSymbols_MessageFormat)),
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

    public static readonly DiagnosticDescriptor CannotInferProductionFactoryParameters = Create(
        id: "FARKLE1009",
        title: Localize(nameof(Resources.CannotInferProductionFactoryParameters_Title)),
        messageFormat: Localize(nameof(Resources.CannotInferProductionFactoryParameters_MessageFormat)),
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Localize(nameof(Resources.CannotInferProductionFactoryParameters_Description)));

    public static readonly SuppressionDescriptor IGrammarSymbolUpcastSuppressor = new(
        id: "FARKLE2001",
        suppressedDiagnosticId: "IDE0004",
        justification: Localize(nameof(Resources.IGrammarSymbolUpcastSuppressor_Justification)));

    public static readonly SuppressionDescriptor PrecompilerInputMethodUnusedSuppressor = new(
        id: "FARKLE2002",
        suppressedDiagnosticId: "IDE0051",
        justification: Localize(nameof(Resources.PrecompilerInputMethodUnusedSuppressor_Justification)));
}
