// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Represents a state of an <see cref="LrStateMachine"/>.
/// </summary>
public readonly struct LrState
{
    private readonly LrStateMachine _lr;

    /// <summary>
    /// The index of the <see cref="LrState"/>, starting from 0.
    /// </summary>
    public int StateIndex { get; }

    internal LrState(LrStateMachine lr, int stateIndex)
    {
        _lr = lr;
        StateIndex = stateIndex;
    }

    /// <summary>
    /// The terminals the <see cref="LrState"/> accepts, along
    /// with their respective <see cref="LrTerminalAction"/>.
    /// </summary>
    public ActionCollection Actions => new(_lr, StateIndex);

    /// <summary>
    /// The possible actions when the end of input is reached at the <see cref="LrState"/>.
    /// </summary>
    public EndOfFileActionCollection EndOfFileActions => new(_lr, StateIndex);

    /// <summary>
    /// The GOTO transitions of the <see cref="LrState"/>.
    /// </summary>
    public GotoCollection Gotos => new(_lr, StateIndex);

    /// <summary>
    /// Whether the <see cref="LrState"/> has more than one possible action for a terminal or the end of input.
    /// </summary>
    /// <seealso cref="LrStateMachine.HasConflicts"/>
    public bool HasConflicts => _lr.StateHasConflicts(StateIndex);

    /// <summary>
    /// Contains the terminals an <see cref="LrState"/> accepts,
    /// along with their respective <see cref="LrTerminalAction"/>.
    /// </summary>
    public readonly struct ActionCollection : IReadOnlyCollection<KeyValuePair<TokenSymbolHandle, LrTerminalAction>>
    {
        private readonly LrStateMachine _lr;

        private readonly int _offset;

        /// <inheritdoc/>
        public int Count { get; }

        internal ActionCollection(LrStateMachine lr, int stateIndex)
        {
            _lr = lr;
            (_offset, Count) = lr.GetActionBounds(stateIndex);
        }

        /// <summary>
        /// Gets the collection's enumerator.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<KeyValuePair<TokenSymbolHandle, LrTerminalAction>>
            IEnumerable<KeyValuePair<TokenSymbolHandle, LrTerminalAction>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Used to enumerate an <see cref="ActionCollection"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TokenSymbolHandle, LrTerminalAction>>
        {
            private readonly ActionCollection _collection;

            private int _currentIndex;

            internal Enumerator(ActionCollection collection)
            {
                _collection = collection;
            }

            /// <inheritdoc/>
            public KeyValuePair<TokenSymbolHandle, LrTerminalAction> Current
            {
                get
                {
                    if (_currentIndex == -1)
                    {
                        ThrowHelpers.ThrowInvalidOperationException();
                    }
                    return _collection._lr.GetActionAt(_collection._offset + _currentIndex);
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
    /// Contains the possible actions when the end of input is reached at an <see cref="LrState"/>.
    /// </summary>
    public readonly struct EndOfFileActionCollection : IReadOnlyCollection<LrEndOfFileAction>
    {
        private readonly LrStateMachine _lr;

        private readonly int _offset;

        /// <inheritdoc/>
        public int Count { get; }

        internal EndOfFileActionCollection(LrStateMachine lr, int stateIndex)
        {
            _lr = lr;
            (_offset, Count) = lr.GetEndOfFileActionBounds(stateIndex);
        }

        /// <summary>
        /// Gets the collection's enumerator.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<LrEndOfFileAction> IEnumerable<LrEndOfFileAction>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Used to enumerate an <see cref="EndOfFileActionCollection"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<LrEndOfFileAction>
        {
            private readonly EndOfFileActionCollection _collection;

            private int _currentIndex;

            internal Enumerator(EndOfFileActionCollection collection)
            {
                _collection = collection;
            }

            /// <inheritdoc/>
            public LrEndOfFileAction Current
            {
                get
                {
                    if (_currentIndex == -1)
                    {
                        ThrowHelpers.ThrowInvalidOperationException();
                    }
                    return _collection._lr.GetEndOfFileActionAt(_collection._offset + _currentIndex);
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
    /// Contains pairs of <see cref="NonterminalHandle"/>s and their respective GOTO destination states.
    /// </summary>
    public readonly struct GotoCollection : IReadOnlyCollection<KeyValuePair<NonterminalHandle, int>>
    {
        private readonly LrStateMachine _lr;

        private readonly int _offset;

        /// <inheritdoc/>
        public int Count { get; }

        internal GotoCollection(LrStateMachine lr, int stateIndex)
        {
            _lr = lr;
            (_offset, Count) = lr.GetGotoBounds(stateIndex);
        }

        /// <summary>
        /// Gets the collection's enumerator.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<KeyValuePair<NonterminalHandle, int>>
            IEnumerable<KeyValuePair<NonterminalHandle, int>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Used to enumerate a <see cref="GotoCollection"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<NonterminalHandle, int>>
        {
            private readonly GotoCollection _collection;

            private int _currentIndex;

            internal Enumerator(GotoCollection collection)
            {
                _collection = collection;
            }

            /// <inheritdoc/>
            public KeyValuePair<NonterminalHandle, int> Current
            {
                get
                {
                    if (_currentIndex == -1)
                    {
                        ThrowHelpers.ThrowInvalidOperationException();
                    }
                    return _collection._lr.GetGotoAt(_collection._offset + _currentIndex);
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
