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
public readonly struct ProductionMemberList : IReadOnlyList<EntityHandle>
{
    private readonly Grammar _grammar;

    private readonly uint _offset;

    /// <inheritdoc/>
    public int Count { get; }

    /// <summary>
    /// Gets the production member at the specified index.
    /// </summary>
    /// <param name="index">The index of the production member.</param>
    public EntityHandle this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
            }

            return _grammar.GrammarTables.GetProductionMemberMember(_grammar.GrammarFile, (uint)index + _offset);
        }
    }

    internal ProductionMemberList(Grammar grammar, uint offset, int count)
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
    /// Used to enumerate a <see cref="ProductionMemberList"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<EntityHandle>
    {
        private readonly ProductionMemberList _collection;
        private int _currentIndex = -1;

        internal Enumerator(ProductionMemberList collection)
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
                return _collection[_currentIndex];
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
