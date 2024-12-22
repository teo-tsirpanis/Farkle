// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Contains the members of a <see cref="Production"/>.
/// </summary>
/// <seealso cref="Production.Members"/>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(FlatCollectionProxy<Production, ProductionCollection>))]
public readonly struct ProductionCollection : IReadOnlyCollection<Production>
{
    private readonly Grammar _grammar;

    private readonly uint _offset;

    /// <inheritdoc/>
    public int Count { get; }

    internal ProductionCollection(Grammar grammar, uint offset, int count)
    {
        _grammar = grammar;
        _offset = offset;
        Count = count;
    }

    /// <summary>
    /// Gets the collection's enumerator.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<Production> IEnumerable<Production>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="ProductionCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<Production>
    {
        private readonly ProductionCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(ProductionCollection collection)
        {
            _collection = collection;
        }

        /// <inheritdoc/>
        public Production Current
        {
            get
            {
                if (_currentIndex < 0)
                {
                    ThrowHelpers.ThrowInvalidOperationException();
                }
                return new(_collection._grammar, new((uint)(_collection._offset + _currentIndex)));
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
