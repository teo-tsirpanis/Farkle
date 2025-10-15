// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder.Dfa;

/// <summary>
/// Contains options to customize the DFA building process.
/// </summary>
/// <seealso cref="DfaBuild{TChar}"/>
[Flags]
internal enum DfaBuildOptions : uint
{
    /// <summary>
    /// No options are defined.
    /// </summary>
    None = 0,
    /// <summary>
    /// The generated DFA will match characters case-sensitively, unless a
    /// regex specifies otherwise.
    /// </summary>
    CaseSensitive = 1,
    /// <summary>
    /// Conflicts will be attempted to be resolved by prioritizing symbols.
    /// </summary>
    PrioritizeSymbols = 2,
    /// <summary>
    /// The generated DFA will stop as soon as it reaches an accept state.
    /// </summary>
    LazyMatching = 4,
}
