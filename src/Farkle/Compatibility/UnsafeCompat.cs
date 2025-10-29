// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace Farkle;

internal partial class Compatibility
{
    extension(Unsafe)
    {
        // Because "allows ref struct" cannot be polyfilled, bitcasting ref structs will have
        // to be done with Utilities.BitCastSpan.
        public static TTo BitCast<TFrom, TTo>(TFrom source) => Unsafe.As<TFrom, TTo>(ref source);
    }
}
#endif
