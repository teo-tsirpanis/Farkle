// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace Farkle;

internal partial class Compatibility
{
    extension(Unsafe)
    {
        // Because "allows ref struct" cannot be polyfilled, bitcasting ref structs still has to
        // be done with an ifdef, and pointer casting for frameworks prior to .NET 9.
        public static TTo BitCast<TFrom, TTo>(TFrom source) => Unsafe.As<TFrom, TTo>(ref source);
    }
}
#endif
