// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ComSharp;

[Embedded]
internal enum OutputType
{
    Grammar,
    CharParser,
    CharParserSyntaxChecker,
}

[Embedded]
internal enum DiagnosticSeverity
{
    Verbose = Farkle.Diagnostics.DiagnosticSeverity.Verbose,
    Debug = Farkle.Diagnostics.DiagnosticSeverity.Debug,
    Information = Farkle.Diagnostics.DiagnosticSeverity.Information,
    Warning = Farkle.Diagnostics.DiagnosticSeverity.Warning,
    Error = Farkle.Diagnostics.DiagnosticSeverity.Error,
}

[Embedded]
[Guid("F88B9F4E-B699-41B4-8A27-29FABA3DEC79")]
internal partial interface IPrecompiledGrammar
{
    string? Key { get; }

    // The array is not allowed to be modified. We cannot use ImmutableArray<byte> in the precompiler
    // interface, because a user might depend on a newer version than the SDK, which would cause errors.
    byte[]? GrammarFile { get; }

    int InputMethodMetadataToken { get; }

    IReadOnlyList<(int MetadataToken, OutputType Type)> OutputMethods { get; }
}

[Embedded]
[Guid("1B83A6E6-4098-4EF9-8ED8-CEF712D223C3")]
internal partial interface ILogger
{
    DiagnosticSeverity LogLevel { get; }

    void Log(DiagnosticSeverity severity, object message, string? code);
}

[Embedded]
[Guid("0A05F3AA-3A45-4A56-85FD-0BB0ECEF40DE")]
internal partial interface IPrecompilerOptions
{
    CancellationToken CancellationToken { get; }

    ILogger? Logger { get; }
}

[Embedded]
[Guid("483E7343-4A3F-47E9-8722-43905CAE86D9")]
internal partial interface IPrecompilerInterface
{
    IEnumerable<IPrecompiledGrammar> DiscoverAndPrecompile(Assembly assembly, IPrecompilerOptions? options);
}

[Embedded]
internal sealed partial class PrecompilerInterfaceWrappers : ComSharpWrappers;
