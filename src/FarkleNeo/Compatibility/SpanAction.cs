// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if !(NETCOREAPP || NETSTANDARD2_1_OR_GREATER)
namespace System;

internal delegate void SpanAction<T, TArg>(Span<T> span, TArg arg);
#endif
