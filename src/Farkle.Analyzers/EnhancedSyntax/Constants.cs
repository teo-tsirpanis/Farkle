// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Analyzers.EnhancedSyntax;

public static class Constants
{
    public const string GlobalAlias = "global::";

    public const string IGrammarSymbolName = "Farkle.Builder.IGrammarSymbol";

    public const string IGrammarSymbol1Name = "Farkle.Builder.IGrammarSymbol`1";

    public const string ProductionBuilderFactoryClassName = "Farkle.Builder.Production";

    public const string ProductionBuilderFactoryBuildMethodName = "Build";

    public const string ProductionBuilderFactoryBuildMethodFullName = $"{ProductionBuilderFactoryClassName}.{ProductionBuilderFactoryBuildMethodName}";

    public const string UseEnhancedSyntaxAttributeName = "Farkle.Builder.UseEnhancedSyntaxAttribute";
}
