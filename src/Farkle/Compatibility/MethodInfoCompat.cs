// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET5_0_OR_GREATER
using Microsoft.CodeAnalysis;

namespace System.Reflection;

[Embedded]
internal static class MethodInfoCompat
{
    public static T CreateDelegate<T>(this MethodInfo methodInfo) where T : Delegate => (T)methodInfo.CreateDelegate(typeof(T));
}
#endif
