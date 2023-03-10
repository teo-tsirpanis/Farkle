// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.StateMachines;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a context-free grammar.
/// </summary>
/// <remarks>
/// The grammar's data is internally stored in a binary format described in
/// <see href="https://github.com/teo-tsirpanis/Farkle/blob/mainstream/designs/7.0/grammar-file-format-spec.md"/>
/// </remarks>
public abstract class Grammar
{
    internal readonly StringHeap StringHeap;
    internal readonly BlobHeap BlobHeap;
    internal readonly GrammarTables GrammarTables;
    internal readonly int TerminalCount;

    internal abstract ReadOnlySpan<byte> GrammarFile { get; }

    /// <summary>
    /// The length of the grammar's data in bytes.
    /// </summary>
    public int DataLength => GrammarFile.Length;

    /// <summary>
    /// General information about this <see cref="Grammar"/>.
    /// </summary>
    public GrammarInfo GrammarInfo => new(this);

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="TokenSymbol"/>s
    /// that have the <see cref="TokenSymbolAttributes.Terminal"/> flag set.
    /// </summary>
    public TokenSymbolCollection Terminals => new(this, TerminalCount);

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="TokenSymbol"/>s.
    /// </summary>
    public TokenSymbolCollection TokenSymbols => new(this, GrammarTables.TokenSymbolRowCount);

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="Group"/>s.
    /// </summary>
    public GroupCollection Groups => new(this);

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="Nonterminal"/>s.
    /// </summary>
    public NonterminalCollection Nonterminals => new(this);

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="Production"/>s.
    /// </summary>
    public ProductionCollection Productions => new(this, 1, GrammarTables.ProductionRowCount);

    /// <summary>
    /// The <see cref="Grammar"/>'s <see cref="Dfa{T}"/> on <see cref="char"/>, if it exists.
    /// </summary>
    public Dfa<char>? DfaOnChar { get; }

    /// <summary>
    /// The <see cref="Grammar"/>'s <see cref="StateMachines.LrStateMachine"/>, if it exists.
    /// </summary>
    public LrStateMachine? LrStateMachine { get; }

    private static void ValidateHeader(GrammarHeader header)
    {
        if (header.IsSupported)
        {
            return;
        }

        string errorMessage = header.FileType switch
        {
            GrammarFileType.Farkle when header.VersionMajor > GrammarConstants.VersionMajor => "The grammar's format version is too new for this version of Farkle to support.",
            GrammarFileType.Farkle => "The grammar's format version is too old for this version of Farkle to support.",
            GrammarFileType.EgtNeo => "EGTneo grammar files produced by Farkle 6.x are not supported.",
            GrammarFileType.GoldParser => "Grammar files produced by GOLD Parser must be opened with the Grammar.CreateFromGoldParser method.",
            _ => "Unrecognized file format."
        };
        ThrowHelpers.ThrowNotSupportedException(errorMessage);
    }

    private protected Grammar(ReadOnlySpan<byte> grammarFile)
    {
        GrammarHeader header = GrammarHeader.Read(grammarFile);
        ValidateHeader(header);

        GrammarStreams streams = new(grammarFile, header.StreamCount, out _);

        StringHeap = new(grammarFile, streams.StringHeap);
        BlobHeap = new(streams.BlobHeap);
        GrammarTables = new(grammarFile, streams.TableStream, out _);

        GrammarStateMachines stateMachines = new(grammarFile, in BlobHeap, in GrammarTables, out _);
        (DfaOnChar, LrStateMachine) = StateMachineUtilities.GetGrammarStateMachines(this, grammarFile, in stateMachines);

        bool rejectTerminals = false;
        for (int i = 1; i <= GrammarTables.TokenSymbolRowCount; i++)
        {
            TokenSymbolAttributes flags = GrammarTables.GetTokenSymbolFlags(grammarFile, (uint)i);
            if ((flags & TokenSymbolAttributes.Terminal) != 0)
            {
                if (rejectTerminals)
                {
                    ThrowHelpers.ThrowInvalidDataException("Terminals must come before other token symbols.");
                }
                TerminalCount++;
            }
            else
            {
                rejectTerminals = true;
            }
        }
    }

    /// <summary>
    /// Creates a <see cref="Grammar"/> from a preallocated immutable byte buffer.
    /// </summary>
    /// <param name="grammarData">An <see cref="ImmutableArray{Byte}"/>
    /// containing the grammar's data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grammarData"/> has its
    /// <see cref="ImmutableArray{Byte}.IsDefault"/> property set to <see langword="true"/>.</exception>
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    /// <remarks>
    /// This overload is more efficient because it avoids copying <paramref name="grammarData"/>.
    /// </remarks>
    public static Grammar Create(ImmutableArray<byte> grammarData)
    {
        if (grammarData.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(grammarData));
        }
        return new ManagedMemoryGrammar(grammarData);
    }

    /// <summary>
    /// Creates a <see cref="Grammar"/> from a byte buffer.
    /// </summary>
    /// <param name="grammarData">A <see cref="ReadOnlySpan{Byte}"/>
    /// containing the grammar's data.</param>
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    /// <remarks>
    /// The contents of <paramref name="grammarData"/> will be copied to a new internal buffer.
    /// To limit such data copies use <see cref="Create(ImmutableArray{byte})"/> instead.
    /// </remarks>
    public static Grammar Create(ReadOnlySpan<byte> grammarData)
    {
        return new ManagedMemoryGrammar(grammarData);
    }

    /// <summary>
    /// Returns the string pointed by the given <see cref="StringHandle"/>.
    /// </summary>
    /// <param name="handle">The string handle to retrieve the string from.</param>
    public string GetString(StringHandle handle) => StringHeap.GetString(GrammarFile, handle);

    /// <summary>
    /// Gets the <see cref="TokenSymbol"/> pointed by the given <see cref="TokenSymbolHandle"/>.
    /// </summary>
    /// <param name="handle">A handle to the token symbol.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/>'s
    /// <see cref="TokenSymbolHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="handle"/>
    /// points to a token symbol that does not exist.</exception>
    public TokenSymbol GetTokenSymbol(TokenSymbolHandle handle)
    {
        if (!handle.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(handle));
        }

        if (handle.Value >= GrammarTables.TokenSymbolRowCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle));
        }

        return new(this, handle);
    }

    /// <summary>
    /// Gets the <see cref="Nonterminal"/> pointed by the given <see cref="NonterminalHandle"/>.
    /// </summary>
    /// <param name="handle">A handle to the nonterminal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/>'s
    /// <see cref="NonterminalHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="handle"/>
    /// points to a nonterminal that does not exist.</exception>
    public Nonterminal GetNonterminal(NonterminalHandle handle)
    {
        if (!handle.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(handle));
        }

        if (handle.Value >= GrammarTables.NonterminalRowCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle));
        }

        return new(this, handle);
    }

    /// <summary>
    /// Gets the <see cref="Production"/> pointed by the given <see cref="ProductionHandle"/>.
    /// </summary>
    /// <param name="handle">A handle to the production.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/>'s
    /// <see cref="TokenSymbolHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="handle"/>
    /// points to a production that does not exist.</exception>
    public Production GetProduction(ProductionHandle handle)
    {
        if (!handle.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(handle));
        }

        if (handle.Value >= GrammarTables.ProductionRowCount)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(handle));
        }

        return new(this, handle);
    }

    /// <summary>
    /// Looks up a token symbol or nonterminal with the specified special name.
    /// </summary>
    /// <param name="specialName">The symbol's special name.</param>
    /// <param name="throwIfNotFound">Whether to throw an exception if the symbol was not found.
    /// Defaults to <see true="false"/>.</param>
    /// <returns>An <see cref="EntityHandle"/> containing either a <see cref="TokenSymbolHandle"/>
    /// or a <see cref="NonterminalHandle"/> pointing to the symbol with the specified special name,
    /// or pointing to nothing if the symbol was not found and <paramref name="throwIfNotFound"/>
    /// has a value of <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="specialName"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">The symbol was not found and <paramref name="throwIfNotFound"/>
    /// had a value of <see langword="true"/>.</exception>
    public EntityHandle GetSymbolFromSpecialName(string specialName, bool throwIfNotFound = false)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(specialName);

        ReadOnlySpan<byte> grammarFile = GrammarFile;
        if (StringHeap.LookupString(grammarFile, specialName.AsSpan()) is StringHandle nameHandle)
        {
            for (uint i = 1; i <= GrammarTables.SpecialNameRowCount; i++)
            {
                if (GrammarTables.GetSpecialNameName(grammarFile, i) == nameHandle)
                {
                    return GrammarTables.GetSpecialNameSymbol(grammarFile, i);
                }
            }
        }

        if (throwIfNotFound)
        {
            ThrowHelpers.ThrowKeyNotFoundException("Could not find symbol with the specified special name.");
        }
        return default;
    }

    /// <summary>
    /// Checks whether the given <see cref="TokenSymbolHandle"/> points to a
    /// token symbol with the <see cref="TokenSymbolAttributes.Terminal"/> flag set.
    /// </summary>
    /// <param name="handle">The token symbol handle to check;</param>
    public bool IsTerminal(TokenSymbolHandle handle) => handle.HasValue && handle.Value < TerminalCount;

    /// <summary>
    /// Copies the grammar's data into a new array.
    /// </summary>
    public byte[] ToArray() => GrammarFile.ToArray();

    /// <summary>
    /// Attempts to copy the grammar's data into a <see cref="Span{Byte}"/>.
    /// </summary>
    /// <param name="destination">The span to copy the data to.</param>
    /// <returns>Whether <paramref name="destination"/> was big enough to store the grammar's data.</returns>
    public bool TryCopyDataTo(Span<byte> destination) => GrammarFile.TryCopyTo(destination);

    /// <summary>
    /// Writes the grammar's data to an <see cref="IBufferWriter{Byte}"/>.
    /// </summary>
    /// <param name="buffer">The buffer writer to write the data to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/>
    /// is <see langword="null"/>.</exception>
    public void WriteDataTo(IBufferWriter<byte> buffer)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(buffer);
        buffer.Write(GrammarFile);
    }

    private sealed class ManagedMemoryGrammar : Grammar
    {
        private readonly ImmutableArray<byte> _grammarFile;

        internal override ReadOnlySpan<byte> GrammarFile
        {
            get
            {
                // During construction the `GrammarFile` property has not yet been assigned. This assert makes sure that it is not accessed.
                Debug.Assert(!_grammarFile.IsDefault);
                return _grammarFile.AsSpan();
            }
        }

        public ManagedMemoryGrammar(ImmutableArray<byte> grammarFile) : base(grammarFile.AsSpan())
        {
            _grammarFile = grammarFile;
        }

        public ManagedMemoryGrammar(ReadOnlySpan<byte> grammarFile) : base(grammarFile)
        {
            // To support in the future unchecked indexing into the file,
            // we must load the grammar in memory we own.
            _grammarFile = grammarFile.ToImmutableArray();
        }
    }
}
