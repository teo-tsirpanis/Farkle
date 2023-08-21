// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Farkle;

internal static class Utilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T UnsafeCast<T>(object o) where T : class
    {
        Debug.Assert(o is T);
        return Unsafe.As<T>(o);
    }
}
