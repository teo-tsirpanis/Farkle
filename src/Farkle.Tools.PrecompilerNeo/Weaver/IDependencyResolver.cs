// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Mono.Cecil;

namespace Farkle.Tools.Precompiler.Weaver;

public interface IDependencyResolver
{
    AssemblyNameDefinition? Resolve(string assemblyName);
}
