// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents the kind of a token symbol in a grammar to be built.
/// </summary>
internal enum TokenSymbolKind
{
    Terminal = 0,
    Noise = 1,
    GroupStart = 2,
    GroupEnd = 3
}
