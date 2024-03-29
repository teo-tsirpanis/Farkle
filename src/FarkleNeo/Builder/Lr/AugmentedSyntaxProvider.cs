// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;
using System.Text;

namespace Farkle.Builder.Lr;

/// <summary>
/// Provides syntax information for an augmented grammar and an easier API, on
/// top of <see cref="IGrammarSyntaxProvider"/>.
/// </summary>
/// <param name="provider">The original grammar syntax provider.</param>
/// <remarks>
/// The parser tables generation algorithm requires the grammar
/// to be augmented with:
/// <list type="bullet">
/// <item>A new start symbol, <c>S'</c>.</item>
/// <item>A terminal, <c>#</c>, to mark the end of the input.</item>
/// <item>A production <c>S' → S #</c>, where <c>S</c> is the original start symbol.</item>
/// </list>
/// </remarks>
internal readonly struct AugmentedSyntaxProvider(IGrammarSyntaxProvider provider)
{
    private readonly IGrammarSyntaxProvider _provider = provider;

    public int TerminalCount => _provider.TerminalCount + 1;

    public int NonterminalCount => _provider.NonterminalCount + 1;

    public int ProductionCount => _provider.ProductionCount + 1;

    public const int StartSymbolIndex = 0;

    public Symbol StartSymbol => Symbol.CreateNonterminal(StartSymbolIndex, this);

    public const int EndSymbolIndex = 0;

    public Symbol EndSymbol => Symbol.CreateTerminal(EndSymbolIndex, this);

    /// <summary>
    /// The index of <see cref="StartProduction"/>.
    /// </summary>
    public const int StartProductionIndex = 0;

    /// <summary>
    /// The starting <c>S' → S #</c> production of the grammar.
    /// </summary>
    public Production StartProduction => new(StartProductionIndex, this);

    public ProductionCollection AllProductions => new(0, ProductionCount, this);

    public string GetTerminalName(int index) => index == 0 ? "(EOF)" : _provider.GetTerminalName(index - 1);

    public string GetNonterminalName(int index) => index == 0 ? "S'" : _provider.GetNonterminalName(index - 1);

    private (int FirstProduction, int ProductionCount) GetNonterminalProductions(int nonterminalIndex)
    {
        if (nonterminalIndex == StartSymbolIndex)
        {
            return (StartProductionIndex, 1);
        }
        else
        {
            var (first, count) = _provider.GetNonterminalProductions(nonterminalIndex - 1);
            return (first + 1, count);
        }
    }

    public ProductionCollection EnumerateNonterminalProductions(int nonterminalIndex)
    {
        var (first, count) = GetNonterminalProductions(nonterminalIndex);
        return new(first, count, this);
    }

    public Symbol GetProductionHead(int productionIndex) => productionIndex == StartProductionIndex
        ? StartSymbol
        : Symbol.CreateNonterminal(_provider.GetProductionHead(productionIndex - 1) + 1, this);

    private (int FirstMember, int MemberCount) GetProductionMembersBounds(int index)
    {
        if (index == StartProductionIndex)
        {
            return (0, 1);
        }
        var (first, count) = _provider.GetProductionMembers(index - 1);
        return (first + 1, count);
    }

    public ProductionMemberList GetProductionMembers(Production production)
    {
        var (first, count) = GetProductionMembersBounds(production.Index);
        return new(first, count, this);
    }

    private Symbol GetProductionMember(int memberIndex)
    {
        switch (memberIndex)
        {
            case 0:
                return Symbol.CreateTerminal(_provider.StartSymbol, this);
            case 1:
                return EndSymbol;
            default:
                var (symbolIndex, isTerminal) = _provider.GetProductionMember(memberIndex - 2);
                return Symbol.Create((symbolIndex + 1, isTerminal), this);
        }
    }

    /// <summary>
    /// Represents a terminal or nonterminal symbol in an augmented grammar.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct Symbol(uint value, AugmentedSyntaxProvider syntax) : IEquatable<Symbol>, IComparable<Symbol>
    {
        private readonly uint _value = value;

        private const uint ValueMask = 0x80000000;

#if DEBUG
        private readonly AugmentedSyntaxProvider _debugOnlySyntax = syntax;

        private string DebuggerDisplay => IsTerminal ?
            _debugOnlySyntax.GetTerminalName(Index) :
            $"<{_debugOnlySyntax.GetNonterminalName(Index)}>";
#else
        private string DebuggerDisplay => IsTerminal ?
            $"Terminal {Index}" :
            $"Nonterminal {Index}";
#endif

        public bool IsTerminal => _value < ValueMask;

        public int Index => (int)(_value & ~ValueMask);

        public static Symbol Create((int Index, bool IsTerminal) symbol, AugmentedSyntaxProvider syntax) =>
            new((uint)symbol.Index | (symbol.IsTerminal ? 0 : ValueMask), syntax);

        public static Symbol CreateTerminal(int index, AugmentedSyntaxProvider syntax) => Create((index, true), syntax);

        public static Symbol CreateNonterminal(int index, AugmentedSyntaxProvider syntax) => Create((index, false), syntax);

        public bool Equals(Symbol other) => _value == other._value;

        public override bool Equals(object? obj) => obj is Symbol x && Equals(x);

        public override int GetHashCode() => _value.GetHashCode();

        /// <summary>
        /// Compares two <see cref="Symbol"/>s.
        /// </summary>
        /// <remarks>
        /// Terminals are always ordered before nonterminals, regardless of their indices.
        /// </remarks>
        public int CompareTo(Symbol other) => _value.CompareTo(other._value);
    }

    [DebuggerTypeProxy(typeof(FlatCollectionProxy<Production, ProductionCollection>))]
    public struct ProductionCollection(int firstProduction, int count, AugmentedSyntaxProvider syntax) : IEnumerable<Production>, IEnumerator<Production>
    {
        private readonly AugmentedSyntaxProvider _syntax = syntax;

        private readonly int _firstProduction = firstProduction, _count = count;

        private int _index = -1;

        readonly object IEnumerator.Current => Current;

        public readonly Production Current
        {
            get
            {
                Debug.Assert(_index >= 0);
                return new(_firstProduction + _index, _syntax);
            }
        }

        public readonly void Dispose() { }

        readonly IEnumerator<Production> IEnumerable<Production>.GetEnumerator() => GetEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public readonly ProductionCollection GetEnumerator() => this;

        public bool MoveNext()
        {
            if (_index < _count - 1)
            {
                _index++;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Reset() => _index = -1;
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct Production(int index, AugmentedSyntaxProvider syntax) : IEquatable<Production>
    {
        public int Index { get; } = index;

#if DEBUG
        private readonly AugmentedSyntaxProvider _debugOnlySyntax = syntax;

        public readonly string GetDebuggerDisplay(int dotPosition = -1)
        {
            var sb = new StringBuilder();
            sb.Append($"<{_debugOnlySyntax.GetNonterminalName(_debugOnlySyntax.GetProductionHead(Index).Index)}> →");
            var members = _debugOnlySyntax.GetProductionMembers(this);
            for (int i = 0; i < members.Count; i++)
            {
                if (i == dotPosition)
                {
                    sb.Append(" •");
                }
                sb.Append(' ');
                var member = members[i];
                if (member.IsTerminal)
                {
                    sb.Append(Grammars.TokenSymbol.FormatName(_debugOnlySyntax.GetTerminalName(member.Index)));
                }
                else
                {
                    sb.Append($"<{_debugOnlySyntax.GetNonterminalName(member.Index)}>");
                }
            }
            return sb.ToString();
        }

        private readonly string DebuggerDisplay => GetDebuggerDisplay();
#else
        private readonly string DebuggerDisplay => "Production " + Index;
#endif

        public bool Equals(Production other) => Index == other.Index;

        public override bool Equals(object? obj) => obj is Production x && Equals(x);

        public override int GetHashCode() => Index.GetHashCode();
    }

    [DebuggerTypeProxy(typeof(FlatCollectionProxy<Symbol, ProductionMemberList>))]
    public struct ProductionMemberList(int firstMember, int count, AugmentedSyntaxProvider syntax) : IReadOnlyList<Symbol>, IEnumerator<Symbol>
    {
        private readonly AugmentedSyntaxProvider _syntax = syntax;

        private readonly int _firstMember = firstMember, _count = count;

        private int _index = -1;

        public readonly Symbol this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < _count);
                return _syntax.GetProductionMember(_firstMember + index);
            }
        }

        public readonly int Count => _count;

        readonly object IEnumerator.Current => Current;

        public readonly Symbol Current => this[_index];

        public readonly void Dispose() { }

        public readonly ProductionMemberList GetEnumerator() => this;

        readonly IEnumerator<Symbol> IEnumerable<Symbol>.GetEnumerator() => GetEnumerator();

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool MoveNext()
        {
            if (_index < _count - 1)
            {
                _index++;
                return true;
            }
            return false;
        }

        public void Reset() => _index = -1;
    }
}
