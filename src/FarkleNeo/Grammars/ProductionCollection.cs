// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;

namespace Farkle.Grammars;

/// <summary>
/// Contains the members of a <see cref="Production"/>.
/// </summary>
/// <seealso cref="Production.Members"/>
public readonly struct ProductionCollection : IReadOnlyCollection<EntityHandle>
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

    IEnumerator<EntityHandle> IEnumerable<EntityHandle>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="ProductionMemberCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<EntityHandle>
    {
        private readonly ProductionMemberCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(ProductionMemberCollection collection)
        {
            _collection = collection;
        }

        /// <inheritdoc/>
        public EntityHandle Current
        {
            get
            {
                if (_currentIndex < 0)
                {
                    ThrowHelpers.ThrowInvalidOperationException();
                }
                Grammar grammar = _collection._grammar;
                return grammar.GrammarTables.GetProductionMemberMember(grammar.GrammarFile, (uint)(_collection._offset + _currentIndex));
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            int nextIndex = _currentIndex + 1;
            if (_currentIndex < _collection.Count)
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
