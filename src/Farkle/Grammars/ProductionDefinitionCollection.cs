// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Contains the members of a <see cref="ProductionDefinition"/>.
/// </summary>
/// <seealso cref="ProductionDefinition.Members"/>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(FlatCollectionProxy<ProductionDefinition, ProductionDefinitionCollection>))]
public readonly struct ProductionDefinitionCollection : IReadOnlyCollection<ProductionDefinition>
{
    private readonly Grammar _grammar;

    private readonly uint _offset;

    /// <inheritdoc/>
    public int Count { get; }

    internal ProductionDefinitionCollection(Grammar grammar, uint offset, int count)
    {
        _grammar = grammar;
        _offset = offset;
        Count = count;
    }

    /// <summary>
    /// Gets the collection's enumerator.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<ProductionDefinition> IEnumerable<ProductionDefinition>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Used to enumerate a <see cref="ProductionDefinitionCollection"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<ProductionDefinition>
    {
        private readonly ProductionDefinitionCollection _collection;
        private int _currentIndex = -1;

        internal Enumerator(ProductionDefinitionCollection collection)
        {
            _collection = collection;
        }

        /// <inheritdoc/>
        public ProductionDefinition Current
        {
            get
            {
                if (_currentIndex < 0)
                {
                    ThrowHelpers.ThrowInvalidOperationException();
                }
                return new(_collection._grammar, new((uint)(_collection._offset + _currentIndex)));
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
