// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Represents an LR(1) state machine stored in a <see cref="Grammars.Grammar"/>.
/// It is used by parsers to parse tokens produced by tokenizers.
/// </summary>
[DebuggerDisplay("Count = {Count}; HasConflicts = {HasConflicts}}")]
[DebuggerTypeProxy(typeof(FlatCollectionProxy<LrState, LrStateMachine>))]
public abstract class LrStateMachine : IReadOnlyList<LrState>
{
    internal LrStateMachine(int count, bool hasConflicts)
    {
        Count = count;
        HasConflicts = hasConflicts;
    }

    internal abstract Grammar Grammar { get; }

    internal abstract (int Offset, int Count) GetActionBounds(int state);

    internal abstract KeyValuePair<TokenSymbolHandle, LrAction> GetActionAt(int state);

    internal abstract (int Offset, int Count) GetEndOfFileActionBounds(int state);

    internal abstract LrEndOfFileAction GetEndOfFileActionAt(int index);

    internal abstract (int Offset, int Count) GetGotoBounds(int state);

    internal abstract KeyValuePair<NonterminalHandle, int> GetGotoAt(int index);

    internal abstract void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables);

    internal virtual bool StateHasConflicts(int state)
    {
        if (GetEndOfFileActionBounds(state).Count > 1)
        {
            return true;
        }

        (int offset, int count) = GetActionBounds(state);
        if (count <= 1)
        {
            return false;
        }
        TokenSymbolHandle previousTerminal = GetActionAt(offset).Key;
        for (int i = 1; i < count; i++)
        {
            TokenSymbolHandle terminal = GetActionAt(offset + i).Key;
            if (terminal == previousTerminal)
            {
                return true;
            }
            previousTerminal = terminal;
        }

        return false;
    }

    /// <summary>
    /// The number of the <see cref="LrStateMachine"/>'s initial state.
    /// </summary>
    public int InitialState => 0;

    /// <summary>
    /// The number of states in the <see cref="LrStateMachine"/>.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Whether there might be at least one state in the <see cref="LrStateMachine"/> with
    /// more than one possible actions for the same terminal, or the end of input.
    /// </summary>
    /// <remarks>
    /// <para>Parsers can use this property to quickly determine if the state machine is usable for parsing.</para>
    /// <para>Note that on some pathological grammar files it is possible for a state machine to not
    /// have conflicts and this property to have a value of <see langword="true"/>, but a value of
    /// <see langword="false"/> guarantees that it doesn't have conflicts. Farkle does not treat
    /// such state machines as suitable for parsing.</para>
    /// </remarks>
    public bool HasConflicts { get; }

    /// <summary>
    /// Gets the <see cref="LrState"/> of the <see cref="LrStateMachine"/> with the specified index.
    /// </summary>
    /// <param name="index">The state's index, starting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is
    /// less than zero or greater than or equal to <see cref="Count"/>.</exception>
    public LrState this[int index]
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
    /// Gets the next action from a state, when the given terminal is encountered.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="terminal">The terminal that was encountered.</param>
    /// <exception cref="NotSupportedException">The <see cref="LrStateMachine"/> has conflicts.</exception>
    /// <remarks>This method is intended to be used by parsers.</remarks>
    public abstract LrAction GetAction(int state, TokenSymbolHandle terminal);

    /// <summary>
    /// Gets the next action from a state, when the end of the input stream is reached.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <exception cref="NotSupportedException">The <see cref="LrStateMachine"/> has conflicts.</exception>
    /// <remarks>This method is intended to be used by parsers.</remarks>
    public abstract LrEndOfFileAction GetEndOfFileAction(int state);

    /// <summary>
    /// Performs a GOTO transition from one state to another, based on a nonterminal produced by a reduction.
    /// </summary>
    /// <param name="state">The index of the current state.</param>
    /// <param name="nonterminal">The nonterminal that was produced.</param>
    /// <returns>The index of the state to go to.</returns>
    /// <exception cref="KeyNotFoundException">A GOTO was not found for this state and nonterminal.
    /// Properly written parsers and grammar files should not encounter this exception.</exception>
    /// <remarks>This method is intended to be used by parsers.</remarks>
    public abstract int GetGoto(int state, NonterminalHandle nonterminal);

    /// <summary>
    /// Gets the enumerator of the <see cref="LrStateMachine"/>'s states.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<LrState> IEnumerable<LrState>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate the states of an <see cref="LrStateMachine"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<LrState>
    {
        private readonly LrStateMachine _lr;
        private int _currentIndex = -1;

        internal Enumerator(LrStateMachine lr)
        {
            _lr = lr;
        }

        /// <inheritdoc/>
        public LrState Current => _lr[_currentIndex];

        /// <inheritdoc/>
        public bool MoveNext()
        {
            int nextIndex = _currentIndex + 1;
            if (nextIndex < _lr.Count)
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
