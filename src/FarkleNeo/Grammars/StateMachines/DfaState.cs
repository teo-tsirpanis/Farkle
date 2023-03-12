// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;
using System.Text;

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
[DebuggerDisplay("{DebuggerDisplay(),nq}")]
[DebuggerTypeProxy(typeof(DfaStateProxy<>))]
public readonly struct DfaState<TChar>
{
    private readonly Dfa<TChar> _dfa;

    internal Grammar Grammar => _dfa.Grammar;

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
    /// The <see cref="DfaState{TChar}"/>'s possible accept symbols.
    /// </summary>
    /// <seealso cref="HasConflicts"/>
    public AcceptSymbolCollection AcceptSymbols
    {
        get
        {
            (int offset, int count) = _dfa.GetAcceptSymbolBounds(StateIndex);
            return new(_dfa, offset, count);
        }
    }

    private string DebuggerDisplay()
    {
        var sb = new StringBuilder();

        sb.Append($"{Edges.Count} edges");
        switch (AcceptSymbols.Count)
        {
            case 1:
                sb.Append(", accept");
                break;
            case > 1:
                sb.Append($", accept({AcceptSymbols.Count})");
                break;
        }

        return sb.ToString();
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
    /// <seealso cref="Dfa{TChar}.HasConflicts"/>
    public bool HasConflicts => _dfa.StateHasConflicts(StateIndex);

    /// <summary>
    /// Contains the edges of a <see cref="DfaState{TChar}"/>.
    /// </summary>
    [DebuggerTypeProxy(typeof(DfaEdgesProxy<>))]
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

            private int _currentIndex = -1;

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
                    return _collection._dfa.GetEdgeAt(_collection._offset + _currentIndex);
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

    /// <summary>
    /// Contains the accept symbols of a <see cref="DfaState{TChar}"/>.
    /// </summary>
    [DebuggerTypeProxy(typeof(DfaAcceptSymbolsProxy<>))]
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

            private int _currentIndex = -1;

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
                    return _collection._dfa.GetAcceptSymbolAt(_collection._offset + _currentIndex);
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
}
