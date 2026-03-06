// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

// Contains a .NET object model of the precompiler interface.
// We can't return the COM# interfaces because they are internal, so we have to add yet another layer
// of indirection.
// We could directly work with the COM# objects if Farkle.Tools.MSBuild was rewritten to C# and had
// Farkle.Tools.Precompiler merged to it, but even then, it would keep the user assembly ALC loaded
// for longer than before.

namespace Farkle.Tools.Precompiler;

public enum OutputType
{
    Grammar = ComSharp.OutputType.Grammar,
    CharParser = ComSharp.OutputType.CharParser,
    CharParserSyntaxChecker = ComSharp.OutputType.CharParserSyntaxChecker,
}

public sealed class PrecompiledGrammar
{
    public string? Key { get; }

    public ImmutableArray<byte> GrammarFile { get; }

    public int InputMethodMetadataToken { get; }

    public ReadOnlyCollection<(int MetadataToken, OutputType Type)> OutputMethods { get; }

    internal PrecompiledGrammar(ComSharp.IPrecompiledGrammar grammar)
    {
        Key = grammar.Key;
        GrammarFile = ImmutableCollectionsMarshal.AsImmutableArray(grammar.GrammarFile);
        InputMethodMetadataToken = grammar.InputMethodMetadataToken;
        OutputMethods = new(grammar.OutputMethods.Select(x => (x.MetadataToken, (OutputType)x.Type)).ToList());
    }
}
