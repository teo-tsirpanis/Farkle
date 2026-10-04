// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;

#pragma warning disable RS2008 // Enable analyzer release tracking

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
            helpLinkUri: null, customTags);

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

    public static readonly DiagnosticDescriptor Interface_CannotBeGeneric = Create(
        id: "COMSHARP0003",
        title: "COM# interface must not be generic",
        messageFormat: "{0}: COM# interface must not be generic",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Interface_MustHaveGuid = Create(
        id: "COMSHARP0004",
        title: "COM# interface must have a GUID",
        messageFormat: "{0}: COM# interface must have a GUID",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Interface_MustHaveEmbeddedAttribute = Create(
        id: "COMSHARP0005",
        title: "COM# interface must be applied with [Embedded] attribute",
        messageFormat: "{0}: COM# interface must be applied with [Embedded] attribute",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Interface_MustNotBeDeclaredInMultipleParts = Create(
        id: "COMSHARP0006",
        title: "COM# interface must not be declared in multiple parts",
        messageFormat: "{0}: COM# interface must not be declared in multiple parts",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "In order to determine a stable ordering of methods, COM# interfaces must not be declared in multiple parts."
    );

    public static readonly DiagnosticDescriptor Method_CannotBeGeneric = Create(
        id: "COMSHARP0007",
        title: "Method in COM# interface cannot be generic",
        messageFormat: "{0}: Method in COM# interface cannot be generic",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Method_CannotBeVararg = Create(
        id: "COMSHARP0008",
        title: "Method in COM# interface cannot be vararg",
        messageFormat: "{0}: Method in COM# interface cannot be vararg",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Parameter_CannotBeByReference = Create(
        id: "COMSHARP0009",
        title: "Type cannot be passed by reference",
        messageFormat: "{0}: Type cannot be passed by reference",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Parameter_UnsupportedType = Create(
        id: "COMSHARP0010",
        title: "Type cannot be marshalled",
        messageFormat: "{0}: Type cannot be marshalled",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Feature_Unsupported = Create(
        id: "COMSHARP0011",
        title: "Feature is unsupported",
        messageFormat: "{0}: Feature '{1}' is not yet supported by the COM# source generator",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
