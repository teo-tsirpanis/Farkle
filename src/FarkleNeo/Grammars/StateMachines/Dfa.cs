// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Represents a deterministic finite automaton (DFA) stored in a <see cref="Grammars.Grammar"/>.
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
[DebuggerDisplay("Count = {Count}; HasConflicts = {HasConflicts}}")]
[DebuggerTypeProxy(typeof(DfaProxy<>))]
public abstract class Dfa<TChar> : IReadOnlyList<DfaState<TChar>>
{
    internal Dfa(int stateCount, bool hasConflicts)
    {
        Count = stateCount;
        HasConflicts = hasConflicts;
    }

    internal abstract Grammar Grammar { get; }

    internal abstract (int Offset, int Count) GetAcceptSymbolBounds(int state);

    internal abstract TokenSymbolHandle GetAcceptSymbolAt(int index);

    internal abstract int GetDefaultTransition(int state);

    internal abstract (int Offset, int Count) GetEdgeBounds(int state);

    internal abstract DfaEdge<TChar> GetEdgeAt(int index);

    internal virtual bool StateHasConflicts(int state) => GetAcceptSymbolBounds(state).Count > 1;

    internal abstract void ValidateContent(ReadOnlySpan<byte> grammarFile, in GrammarTables grammarTables);

    /// <summary>
    /// The number of the <see cref="Dfa{TChar}"/>'s initial state.
    /// </summary>
    public int InitialState => 0;

    /// <summary>
    /// The number of states in the <see cref="Dfa{TChar}"/>.
    /// </summary>
    public int Count { get; }

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
    public bool HasConflicts { get; }

    /// <summary>
    /// Gets the <see cref="DfaState{TChar}"/> of the <see cref="Dfa{TChar}"/> with the specified index.
    /// </summary>
    /// <param name="index">The state's index, starting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is
    /// less than zero or greater than or equal to <see cref="Count"/>.</exception>
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
    /// Uses the <see cref="Dfa{TChar}"/> to match a sequence of characters.
    /// </summary>
    /// <param name="grammarFile">A span with the grammar's data</param>
    /// <param name="chars">The characters to match.</param>
    /// <param name="isFinal">Whether there will be no more characters in the
    /// input stream after <paramref name="chars"/>.</param>
    /// <param name="ignoreLeadingErrors">Whether to ignore lexical errors at the
    /// beginning of <paramref name="chars"/>.</param>
    /// <returns>
    /// A tuple with:
    /// <list type="bullet">
    /// <item>A <see cref="TokenSymbolHandle"/> containing the token symbol that was found,
    /// or containing no value in case of a lexical error.</item>
    /// <item>The number of characters that were read before matching a token or encountering
    /// a lexical error.</item>
    /// <item>The state the DFA was at when it stopped..</item>
    /// </list>
    /// </returns>
    internal virtual (TokenSymbolHandle AcceptSymbol, int CharactersRead, int TokenizerState)
        Match(ReadOnlySpan<byte> grammarFile, ReadOnlySpan<TChar> chars, bool isFinal, bool ignoreLeadingErrors = true)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Prepares the <see cref="Dfa{TChar}"/> to be used for parsing
    /// This initializes some lookup tables that speed up <see cref="Match"/>.
    /// </summary>
    internal virtual void PrepareForParsing() { }

    /// <summary>
    /// Gets the enumerator of the <see cref="Dfa{TChar}"/>'s states.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<DfaState<TChar>> IEnumerable<DfaState<TChar>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate the states of a <see cref="Dfa{TChar}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<DfaState<TChar>>
    {
        private readonly Dfa<TChar> _dfa;
        private int _currentIndex = -1;

        internal Enumerator(Dfa<TChar> dfa)
        {
            _dfa = dfa;
        }

        /// <inheritdoc/>
        public DfaState<TChar> Current => _dfa[_currentIndex];

        /// <inheritdoc/>
        public bool MoveNext()
        {
            int nextIndex = _currentIndex + 1;
            if (nextIndex < _dfa.Count)
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
