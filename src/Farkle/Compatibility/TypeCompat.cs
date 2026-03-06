// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET5_0_OR_GREATER
using Microsoft.CodeAnalysis;

namespace System;

[Embedded]
internal static class TypeCompat
{
    public static bool IsAssignableTo(this Type t, Type targetType) => targetType.IsAssignableFrom(t);
}
#endif
