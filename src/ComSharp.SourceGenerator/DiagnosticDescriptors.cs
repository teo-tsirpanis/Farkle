// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

namespace ComSharp;

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

    private const string CategoryUsage = "Usage";

    public static readonly DiagnosticDescriptor GenerateComSharpWrappersAttribute_MustBeOnComSharpWrappers = Create(
        id: "COMSHARP0001",
        title: "'GenerateComSharpWrappersAttribute' must be applied on a class that inherits 'ComSharpWrappers'",
        messageFormat: "'GenerateComSharpWrappersAttribute' must be applied on a class that inherits 'ComSharpWrappers'",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor GenerateComSharpWrappersAttribute_MustSpecifyInterface = Create(
        id: "COMSHARP0002",
        title: "Argument to 'GenerateComSharpWrappersAttribute' must be an interface",
        messageFormat: "Argument to 'GenerateComSharpWrappersAttribute' must be an interface",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
