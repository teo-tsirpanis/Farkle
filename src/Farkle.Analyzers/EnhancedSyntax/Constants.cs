// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Analyzers.EnhancedSyntax;

public static class Constants
{
    public const string GlobalAlias = "global::";

    public const string IGrammarSymbolName = "Farkle.Builder.IGrammarSymbol";

    public const string IGrammarSymbol1Name = "Farkle.Builder.IGrammarSymbol`1";

    public const string ProductionFactoryClassName = "Farkle.Builder.Production";

    public const string ProductionFactoryCreateMethodName = "Create";

    public const string ProductionFactoryCreateMethodFullName = $"{ProductionFactoryClassName}.{ProductionFactoryCreateMethodName}";

    public const string UseEnhancedSyntaxAttributeName = "Farkle.Builder.UseEnhancedSyntaxAttribute";
}
