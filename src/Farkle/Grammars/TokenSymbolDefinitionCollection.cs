// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Contains <see cref="TokenSymbolDefinition"/>s of a <see cref="Grammar"/>.
/// </summary>
/// <seealso cref="Grammar.Terminals"/>
/// <seealso cref="Grammar.TokenSymbols"/>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(FlatCollectionProxy<TokenSymbolDefinition, TokenSymbolCollection>))]
public readonly struct TokenSymbolCollection : IReadOnlyCollection<TokenSymbolDefinition>
{
    private readonly Grammar _grammar;

    /// <inheritdoc/>
    public int Count { get; }

    internal TokenSymbolCollection(Grammar grammar, int count)
    {
        _grammar = grammar;
        Count = count;
    }

    /// <summary>
    /// Gets the collection's enumerator.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<TokenSymbolDefinition> IEnumerable<TokenSymbolDefinition>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="TokenSymbolCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<TokenSymbolDefinition>
    {
        private readonly TokenSymbolCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(TokenSymbolCollection collection)
        {
            _collection = collection;
        }

        /// <inheritdoc/>
        public TokenSymbolDefinition Current
        {
            get
            {
                if (_currentIndex < 0)
                {
                    ThrowHelpers.ThrowInvalidOperationException();
                }
                return new(_collection._grammar, new((uint)(_currentIndex + 1)));
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            int nextIndex = _currentIndex + 1;
            if (nextIndex < _collection.Count)
            {
                _currentIndex = nextIndex;
                return true;
            }
            return false;
        }

        object IEnumerator.Current => Current;

        void IDisposable.Dispose() { }

        void IEnumerator.Reset() => _currentIndex = -1;
    }
}
