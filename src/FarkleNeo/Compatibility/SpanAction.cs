// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

#if NETSTANDARD2_0
namespace System;

internal delegate void SpanAction<T, TArg>(Span<T> span, TArg arg);
#endif
