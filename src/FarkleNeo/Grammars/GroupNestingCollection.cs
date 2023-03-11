// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;

namespace Farkle.Grammars;

/// <summary>
/// Contains the <see cref="Group"/>s that can be nested inside a <see cref="Group"/>.
/// </summary>
/// <seealso cref="Group.Nesting"/>
public readonly struct GroupNestingCollection : IReadOnlyCollection<Group>
{
    private readonly Grammar _grammar;

    private readonly uint _offset;

    /// <inheritdoc/>
    public int Count { get; }

    internal GroupNestingCollection(Grammar grammar, uint offset, int count)
    {
        _grammar = grammar;
        _offset = offset;
        Count = count;
    }

    /// <summary>
    /// Gets the collection's enumerator.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<Group> IEnumerable<Group>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="GroupNestingCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<Group>
    {
        private readonly GroupNestingCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(GroupNestingCollection collection)
        {
            _collection = collection;
        }

        /// <inheritdoc/>
        public Group Current
        {
            get
            {
                if (_currentIndex < 0)
                {
                    ThrowHelpers.ThrowInvalidOperationException();
                }
                Grammar grammar = _collection._grammar;
                return new(grammar, grammar.GrammarTables.GetGroupNestingGroup(grammar.GrammarFile, (uint)(_collection._offset + _currentIndex)));
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
