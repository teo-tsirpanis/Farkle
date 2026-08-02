// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;

namespace Farkle.Analyzers.Tests;

public sealed class FarkleSourceGeneratorTest<TSourceGenerator> : CSharpSourceGeneratorTest<TSourceGenerator, NUnitVerifier>
    where TSourceGenerator : IIncrementalGenerator, new()
{
}
