// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Contains the <see cref="SpecialNameDefinition"/>s of a <see cref="Grammar"/>.
/// </summary>
/// <remarks>
/// This type is intended to be used for presentation purposes only.
/// For maximum performance, parsers are strongly recommended to use
/// <see cref="IGrammarProvider.GetSymbolFromSpecialName"/>, or one of
/// the extension methods in <see cref="GrammarExtensions"/> instead.
/// </remarks>
/// <seealso cref="Grammar.SpecialNameDefinitions"/>
/// <seealso cref="IGrammarProvider.GetSymbolFromSpecialName"/>
/// <seealso cref="GrammarExtensions.GetTokenSymbolFromSpecialName"/>
/// <seealso cref="GrammarExtensions.GetNonterminalFromSpecialName"/>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(FlatCollectionProxy<SpecialNameDefinition, SpecialNameDefinitionCollection>))]
public readonly struct SpecialNameDefinitionCollection : IReadOnlyCollection<SpecialNameDefinition>
{
    private readonly Grammar _grammar;

    /// <inheritdoc/>
    public int Count => _grammar.GrammarTables.SpecialNameRowCount;

    internal SpecialNameDefinitionCollection(Grammar grammar)
    {
        _grammar = grammar;
    }

    /// <summary>
    /// Gets the collection's enumerator.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<SpecialNameDefinition> IEnumerable<SpecialNameDefinition>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="SpecialNameDefinitionCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<SpecialNameDefinition>
    {
        private readonly SpecialNameDefinitionCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(SpecialNameDefinitionCollection collection)
        {
            _collection = collection;
        }

        /// <inheritdoc/>
        public SpecialNameDefinition Current
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
