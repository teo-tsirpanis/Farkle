// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Common;

internal static partial class Compatibility
{
    private static PropertyInfo? s_isUnion => field ??= typeof(ITypeSymbol).GetProperty("IsUnion");

    extension(ITypeSymbol symbol)
    {
        public bool IsUnion => (bool?)s_isUnion?.GetValue(symbol) ?? false;
    }
}
