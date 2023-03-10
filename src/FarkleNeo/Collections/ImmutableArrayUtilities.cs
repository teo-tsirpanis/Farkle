// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Farkle.Collections;

internal static class ImmutableArrayUtilities
{
    public static ImmutableArray<T> Create<T, TState>(int length, TState state, SpanAction<T, TState> action)
    {
        T[] array = new T[length];
        action(array.AsSpan(), state);
        return Unsafe.As<T[], ImmutableArray<T>>(ref array);
    }
}
