// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.Reflection;

namespace Farkle.Tools.Precompiler;

public sealed class PrecompilerResult
{
    /// <summary>
    /// The name of the assembly that contained the logic to build the grammars.
    /// It is either <c>Farkle</c>, or the input assembly itself.
    /// </summary>
    public required AssemblyName FarkleAssemblyName { get; init; }

    public Collection<PrecompiledGrammar> Grammars { get; } = [];
}
