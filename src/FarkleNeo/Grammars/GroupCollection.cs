// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Contains the <see cref="Group"/>s of a <see cref="Grammar"/>.
/// </summary>
/// <seealso cref="Grammar.Groups"/>
[DebuggerDisplay("Count = {Count}")]
public readonly struct GroupCollection : IReadOnlyCollection<Group>
{
    private readonly Grammar _grammar;

    /// <inheritdoc/>
    public int Count => _grammar.GrammarTables.GroupRowCount;

    internal GroupCollection(Grammar grammar)
    {
        _grammar = grammar;
    }

    /// <summary>
    /// Gets the collection's enumerator.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<Group> IEnumerable<Group>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="GroupCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<Group>
    {
        private readonly GroupCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(GroupCollection collection)
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
                return new(_collection._grammar, (uint)(_currentIndex + 1));
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
