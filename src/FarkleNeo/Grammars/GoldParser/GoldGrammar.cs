// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Grammars.GoldParser;

/// <summary>
/// Contains information about a context-free grammar, as understood by GOLD Parser.
/// </summary>
internal sealed class GoldGrammar
{
    public required string Name { get; init; }

    public required ushort StartSymbol { get; init; }

    public required ImmutableArray<(char Start, char End)>[] CharacterSets { get; init; }

    public required Symbol[] Symbols { get; init; }

    public required Group[] Groups { get; init; }

    public required Production[] Productions { get; init; }

    public required DfaState[] DfaStates { get; init; }

    public required ImmutableArray<LalrAction>[] LalrStates { get; init; }

    public enum SymbolKind
    {
        Nonterminal,
        Terminal,
        Noise,
        EndOfFile,
        GroupStart,
        GroupEnd,
        Error
    }

    public sealed class Symbol
    {
        public required string Name { get; init; }

        public required SymbolKind Kind { get; init; }
    }

    public sealed class Group
    {
        public required string Name { get; init; }

        public required ushort ContainerIndex { get; init; }

        public required ushort StartIndex { get; init; }

        public required ushort EndIndex { get; init; }

        public required bool AdvanceByChar { get; init; }

        public required bool KeepEndToken { get; init; }

        public required ImmutableArray<ushort> Nesting { get; init; }
    }

    public sealed class Production
    {
        public required ushort HeadIndex { get; init; }
        public required ImmutableArray<ushort> Members { get; init; }
    }

    public sealed class DfaState
    {
        public required ushort? AcceptIndex { get; init; }

        public required ImmutableArray<(ushort CharSetIndex, ushort TargetStateIndex)> Edges { get; init; }
    }

    public enum LalrActionKind
    {
        Shift,
        Reduce,
        Goto,
        Accept
    }

    public readonly struct LalrAction
    {
        public required ushort SymbolIndex { get; init; }

        public required LalrActionKind Kind { get; init; }

        public required ushort TargetIndex { get; init; }
    }
}
