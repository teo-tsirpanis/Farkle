// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using BitCollections;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents a set of terminals in an augmented grammar.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
internal readonly struct TerminalSet : IEnumerable<Symbol>, IEquatable<TerminalSet>
{
    private readonly BitArrayNeo _value;

#if DEBUG
    private readonly AugmentedSyntaxProvider _debugOnlySyntax;
#endif

    public TerminalSet(AugmentedSyntaxProvider syntax)
    {
        _value = new BitArrayNeo(syntax.TerminalCount);
#if DEBUG
        _debugOnlySyntax = syntax;
#endif
    }

    public TerminalSet(TerminalSet set)
    {
        _value = new BitArrayNeo(set._value);
#if DEBUG
        _debugOnlySyntax = set._debugOnlySyntax;
#endif
    }

    /// <summary>
    /// Whether this value is equal to <see langword="default"/>.
    /// Such values are undefined and must not be used.
    /// </summary>
    public bool IsDefault => _value is null;

    public bool this[Symbol symbol]
    {
        get
        {
            Debug.Assert(symbol.IsTerminal);
            return _value[symbol.Index];
        }
        set
        {
            Debug.Assert(symbol.IsTerminal);
            _value[symbol.Index] = value;
        }
    }

    public bool Set(Symbol symbol, bool value)
    {
        Debug.Assert(symbol.IsTerminal);
        return _value.Set(symbol.Index, value);
    }

    public void SetAll(bool value) => _value.SetAll(value);

    public bool And(TerminalSet other) => _value.And(other._value);

    public bool Or(TerminalSet other) => _value.Or(other._value);

    [ExcludeFromCodeCoverage]
    public string GetDebuggerDisplay()
    {
        if (IsDefault)
        {
            return "<default>";
        }
        var sb = new StringBuilder();
        sb.Append('{');
        bool hasElement = false;
        foreach (var x in _value)
        {
            if (hasElement)
            {
                sb.Append(", ");
            }
            hasElement = true;
#if DEBUG
            var symbol = Symbol.CreateTerminal(x, _debugOnlySyntax);
#else
            var symbol = Symbol.CreateTerminal(x, default);
#endif
            sb.Append(symbol.GetDebuggerDisplay());
        }
        sb.Append('}');
        return sb.ToString();
    }

    public bool Equals(TerminalSet other) => _value.Equals(other._value);

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TerminalSet other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public Enumerator GetEnumerator() => new((_value).GetEnumerator())
    {
#if DEBUG
        DebugOnlySyntax = _debugOnlySyntax,
#endif
    };

    IEnumerator<Symbol> IEnumerable<Symbol>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator(BitCollectionEnumerator enumerator) : IEnumerator<Symbol>
    {
        private BitCollectionEnumerator _enumerator = enumerator;

#if DEBUG
        public required AugmentedSyntaxProvider DebugOnlySyntax { get; init; }

        public readonly Symbol Current => Symbol.CreateTerminal(_enumerator.Current, DebugOnlySyntax);
#else
        public readonly Symbol Current => Symbol.CreateTerminal(_enumerator.Current, default);
#endif

        readonly object IEnumerator.Current => Current;

        public bool MoveNext() => _enumerator.MoveNext();

        public void Reset() => throw new NotSupportedException();

        public readonly void Dispose() { }
    }
}
