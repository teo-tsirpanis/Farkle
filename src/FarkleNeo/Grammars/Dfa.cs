// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;

namespace Farkle.Grammars;

/// <summary>
/// Represents a deterministic finite automaton (DFA) stored in a <see cref="Grammar"/>.
/// It is used by tokenizers to break the input stream into a series of tokens.
/// </summary>
/// <remarks>
/// A DFA is a state machine, with each state containing edges that point to other states
/// based on the current character in the input stream. States can also have an accept
/// symbol, meaning that the tokenizer has at that point encountered an instance of a
/// specific token symbol.
/// </remarks>
/// <typeparam name="TChar">The type of characters the DFA accepts.
/// Typically it is <see cref="char"/> or <see cref="byte"/>.</typeparam>
public abstract class Dfa<TChar> : IReadOnlyList<DfaState<TChar>>
{
    internal Dfa() { }

    internal abstract (int Offset, int Count) GetAcceptSymbolBounds(int state);

    internal abstract TokenSymbolHandle GetAcceptSymbol(int index);

    internal abstract int GetDefaultTransition(int state);

    internal abstract (int Offset, int Count) GetEdgeBounds(int state);

    internal virtual TokenSymbolHandle GetSingleAcceptSymbol(int state)
    {
        switch (GetAcceptSymbolBounds(state))
        {
            case (int offset, 1):
                return GetAcceptSymbol(offset);
            case (_, 0):
                return default;
            default:
                ThrowHelpers.ThrowInvalidOperationException("The DFA state has more than one accept symbol.");
                return default;
        }
    }

    internal abstract DfaEdge<TChar> GetEdge(int index);

    internal virtual bool StateHasConflicts(int state) => GetAcceptSymbolBounds(state).Count > 1;

    /// <summary>
    /// The number of the <see cref="Dfa{TChar}"/>'s initial state.
    /// </summary>
    public int InitialState => 0;

    /// <summary>
    /// The number of states in the <see cref="Dfa{TChar}"/>.
    /// </summary>
    public abstract int Count { get; }

    /// <summary>
    /// Whether there might be at least one state in the <see cref="Dfa{TChar}"/> with
    /// more than one possible accept symbol.
    /// </summary>
    /// <remarks>
    /// <para>Parsers can use this property to quickly determine if the DFA is usable for parsing.</para>
    /// <para>Note that on some pathological grammar files it is possible for a DFA to not have conflicts
    /// and this property to have a value of <see langword="true"/>, but a value of <see langword="false"/>
    /// guarantees that it doesn't have conflicts. Farkle does not treat such DFAs as suitable for parsing.</para>
    /// </remarks>
    public abstract bool HasConflicts { get; }

    /// <summary>
    /// Gets the <see cref="DfaState{TChar}"/> of the <see cref="Dfa{TChar}"/> with the specific index.
    /// </summary>
    /// <param name="index">The state's index, starting from zero.</param>
    public DfaState<TChar> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return new(this, index);
        }
    }

    /// <summary>
    /// Performs a transition from one state to another, based on the current character.
    /// </summary>
    /// <param name="state">The index of the current state.</param>
    /// <param name="c">The current character in the input stream.</param>
    /// <returns>The index of the next state or -1 if such state does not exist.</returns>
    public abstract int NextState(int state, TChar c);

    /// <summary>
    /// Gets the enumerator of the DFA's states.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<DfaState<TChar>> IEnumerable<DfaState<TChar>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate the states of a <see cref="Dfa{TChar}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<DfaState<TChar>>
    {
        private readonly Dfa<TChar> dfa;
        private int _currentIndex = -1;

        internal Enumerator(Dfa<TChar> dfa)
        {
            this.dfa = dfa;
        }

        /// <inheritdoc/>
        public DfaState<TChar> Current => dfa[_currentIndex];

        /// <inheritdoc/>
        public bool MoveNext()
        {
            int nextIndex = _currentIndex + 1;
            if (_currentIndex < dfa.Count)
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
