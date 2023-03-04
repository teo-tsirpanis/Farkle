// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Represents a state of a <see cref="Dfa{TChar}"/>.
/// </summary>
/// <typeparam name="TChar">The type of characters the DFA accepts.
/// Typically it is <see cref="char"/> or <see cref="byte"/>.</typeparam>
/// <remarks>
/// APIs of this type are intended for presentation purposes. To match text against a DFA,
/// use the <see cref="Dfa{TChar}.NextState"/> function instead.
/// </remarks>
public readonly struct DfaState<TChar>
{
    private readonly Dfa<TChar> _dfa;

    /// <summary>
    /// The index of this <see cref="DfaState{TChar}"/>, starting from 0.
    /// </summary>
    public int StateIndex { get; }

    internal DfaState(Dfa<TChar> dfa, int stateIndex)
    {
        _dfa = dfa;
        StateIndex = stateIndex;
    }

    /// <summary>
    /// The <see cref="DfaState{TChar}"/>'s accept symbol, if it exists.
    /// </summary>
    /// <returns>A <see cref="TokenSymbolHandle"/> pointing to the state's accept symbol.
    /// Its <see cref="TokenSymbolHandle.HasValue"/> property will be set to <see langword="false"/>
    /// if the state is not an accept state.</returns>
    /// <exception cref="InvalidOperationException">The state has more than one accept symbols.
    /// This can be checked with the <see cref="HasConflicts"/> property.</exception>
    /// <remarks>
    /// To improve performance, parsers should use this property instead of <see cref="AcceptSymbols"/>.
    /// </remarks>
    /// <seealso cref="HasConflicts"/>
    public TokenSymbolHandle AcceptSymbol => _dfa.GetSingleAcceptSymbol(StateIndex);

    /// <summary>
    /// The <see cref="DfaState{TChar}"/>'s possible accept symbols.
    /// </summary>
    /// <seealso cref="AcceptSymbol"/>
    /// <seealso cref="HasConflicts"/>
    public AcceptSymbolCollection AcceptSymbols
    {
        get
        {
            (int offset, int count) = _dfa.GetAcceptSymbolBounds(StateIndex);
            return new(_dfa, offset, count);
        }
    }

    /// <summary>
    /// The state to go to if a character has no matching edge, or -1 if tokenizing should stop in that case.
    /// </summary>
    public int DefaultTransition => _dfa.GetDefaultTransition(StateIndex);

    /// <summary>
    /// The <see cref="DfaState{TChar}"/>'s edges.
    /// </summary>
    public EdgeCollection Edges
    {
        get
        {
            (int offset, int count) = _dfa.GetEdgeBounds(StateIndex);
            return new(_dfa, offset, count);
        }
    }

    /// <summary>
    /// Whether the <see cref="DfaState{TChar}"/> has more than one possible accept symbol.
    /// </summary>
    /// <remarks>
    /// Accessing the <see cref="AcceptSymbol"/> property while this property
    /// has a value of <see langword="true"/> will cause an exception.
    /// </remarks>
    /// <seealso cref="Dfa{TChar}.HasConflicts"/>
    public bool HasConflicts => _dfa.StateHasConflicts(StateIndex);

    /// <summary>
    /// Contains the edges of a <see cref="DfaState{TChar}"/>.
    /// </summary>
    public readonly struct EdgeCollection : IReadOnlyCollection<DfaEdge<TChar>>
    {
        private readonly Dfa<TChar> _dfa;

        private readonly int _offset;

        /// <inheritdoc/>
        public int Count { get; }

        internal EdgeCollection(Dfa<TChar> dfa, int offset, int count)
        {
            _dfa = dfa;
            _offset = offset;
            Count = count;
        }

        /// <summary>
        /// Gets the collection's enumerator.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<DfaEdge<TChar>> IEnumerable<DfaEdge<TChar>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Used to enumerate an <see cref="EdgeCollection"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<DfaEdge<TChar>>
        {
            private readonly EdgeCollection _collection;

            private int _currentIndex;

            internal Enumerator(EdgeCollection collection)
            {
                _collection = collection;
            }

            /// <inheritdoc/>
            public DfaEdge<TChar> Current
            {
                get
                {
                    if (_currentIndex == -1)
                    {
                        ThrowHelpers.ThrowInvalidOperationException();
                    }
                    return _collection._dfa.GetEdge(_collection._offset + _currentIndex);
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

    /// <summary>
    /// Contains the accept symbols of a <see cref="DfaState{TChar}"/>.
    /// </summary>
    public readonly struct AcceptSymbolCollection : IReadOnlyCollection<TokenSymbolHandle>
    {
        private readonly Dfa<TChar> _dfa;

        private readonly int _offset;

        /// <inheritdoc/>
        public int Count { get; }

        internal AcceptSymbolCollection(Dfa<TChar> dfa, int offset, int count)
        {
            _dfa = dfa;
            _offset = offset;
            Count = count;
        }

        /// <summary>
        /// Gets the collection's enumerator.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<TokenSymbolHandle> IEnumerable<TokenSymbolHandle>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Used to enumerate an <see cref="AcceptSymbolCollection"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TokenSymbolHandle>
        {
            private readonly AcceptSymbolCollection _collection;

            private int _currentIndex;

            internal Enumerator(AcceptSymbolCollection collection)
            {
                _collection = collection;
            }

            /// <inheritdoc/>
            public TokenSymbolHandle Current
            {
                get
                {
                    if (_currentIndex == -1)
                    {
                        ThrowHelpers.ThrowInvalidOperationException();
                    }
                    return _collection._dfa.GetAcceptSymbol(_collection._offset + _currentIndex);
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
}
