// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars.GoldParser;
using Farkle.Grammars.StateMachines;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Farkle.Grammars;

/// <summary>
/// Provides information about a context-free grammar.
/// </summary>
/// <remarks>
/// The grammar's data is internally stored in a binary format described in
/// <see href="https://github.com/teo-tsirpanis/Farkle/blob/mainstream/designs/7.0/grammar-file-format-spec.md"/>
/// </remarks>
public abstract class Grammar : IGrammarProvider
{
    internal readonly StringHeap StringHeap;
    internal readonly BlobHeap BlobHeap;
    internal readonly GrammarTables GrammarTables;

    /// <summary>
    /// A read-only buffer to the <see cref="Grammar"/>'s binary data.
    /// </summary>
    public ReadOnlySpan<byte> Data => GrammarFile;

    internal abstract ReadOnlySpan<byte> GrammarFile { get; }

    /// <summary>
    /// Whether the <see cref="Grammar"/> contains data that are not recognized by this version of Farkle.
    /// </summary>
    public bool HasUnknownData { get; }

    /// <summary>
    /// General information about this <see cref="Grammar"/>.
    /// </summary>
    public GrammarInfo GrammarInfo => new(this);

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="TokenSymbol"/>s
    /// that have the <see cref="TokenSymbolAttributes.Terminal"/> flag set.
    /// </summary>
    public TokenSymbolCollection Terminals => new(this, GrammarTables.TerminalCount);

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
            GrammarFileType.Farkle when header.VersionMajor > GrammarConstants.VersionMajor => Resources.Grammar_TooNewFormat,
            GrammarFileType.Farkle => Resources.Grammar_TooOldFormat,
            GrammarFileType.EgtNeo => Resources.Grammar_EgtNeoNotSupported,
            GrammarFileType.GoldParser => Resources.Grammar_GoldParserMustConvert,
            _ => Resources.Grammar_UnrecognizedFormat
        };
        ThrowHelpers.ThrowNotSupportedException(errorMessage);
    }

    private protected Grammar(ReadOnlySpan<byte> grammarFile)
    {
        GrammarHeader header = GrammarHeader.Read(grammarFile);
        ValidateHeader(header);

        GrammarStreams streams = new(grammarFile, header.StreamCount, out bool hasUnknownStreams);

        StringHeap = new(grammarFile, streams.StringHeap);
        BlobHeap = new(streams.BlobHeap);
        GrammarTables = new(grammarFile, streams.TableStream, out bool hasUnknownTables);

        GrammarStateMachines stateMachines = new(grammarFile, in BlobHeap, in GrammarTables, out bool hasUnknownStateMachines);
        (DfaOnChar, LrStateMachine) = StateMachineUtilities.GetGrammarStateMachines(this, grammarFile, in stateMachines);

        HasUnknownData = header.HasUnknownData || hasUnknownStreams || hasUnknownTables || hasUnknownStateMachines;
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
        ManagedMemoryGrammar grammar = new ManagedMemoryGrammar(grammarData);
        grammar.ValidateContent();
        return grammar;
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
        ManagedMemoryGrammar grammar = new ManagedMemoryGrammar(grammarData);
        grammar.ValidateContent();
        return grammar;
    }

    // Internal for benchmarking purposes.
    // It can be made public once a [RequiresUnsafe] attribute is added.
    internal static Grammar CreateUnsafe(ImmutableArray<byte> grammarData)
    {
        if (grammarData.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(grammarData));
        }
        return new ManagedMemoryGrammar(grammarData);
    }

    /// <summary>
    /// Creates a <see cref="Grammar"/> from a file. The entire file is read in memory.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public static Grammar CreateFromFile(string path)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(path);
        ImmutableArray<byte> data;
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        // If the file is very big, read only a part of it to make
        // sure it has a valid header, before reading the entire file.
        using (Stream file = File.OpenRead(path))
        {
            if (file.Length > 4096)
            {
                Span<byte> buffer = stackalloc byte[GrammarHeader.MinHeaderDisambiguatorSize];
                int nRead = file.ReadAtLeast(buffer, buffer.Length);
                GrammarHeader header = GrammarHeader.Read(buffer);
                ValidateHeader(header);
                file.Position = 0;
            }
            byte[] dataArray = new byte[file.Length];
            file.ReadExactly(dataArray);
            data = ImmutableCollectionsMarshal.AsImmutableArray(dataArray);
        }
#else
        data = ImmutableCollectionsMarshal.AsImmutableArray(File.ReadAllBytes(path));
#endif
        return Create(data);
    }

    /// <summary>
    /// Converts a grammar file produced by GOLD Parser into a <see cref="Grammar"/>.
    /// </summary>
    /// <param name="grammarFile">A <see cref="Stream"/> containing the GOLD Parser grammar file.</param>
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    /// <remarks>
    /// Both Enhanced Grammar Tables (EGT) and Compiled Grammar Tables (CGT) files are supported.
    /// </remarks>
    public static Grammar CreateFromGoldParserGrammar(Stream grammarFile)
    {
        GoldGrammar grammar = GoldGrammarReader.ReadGrammar(grammarFile);
        ImmutableArray<byte> data;
        try
        {
            data = GoldGrammarConverter.Convert(grammar);
        }
        catch (Exception e)
        {
            // Let's provide a unified experience for any exceptions
            // that might be thrown, with a localized message.
            // We cover only Convert to avoid wrapping I/O errors.
            throw new InvalidDataException(Resources.Grammar_FailedToConvert, e);
        }
        return Create(data);
    }

    internal Dfa<TChar>? GetDfa<TChar>()
    {
        if (typeof(TChar) == typeof(char))
        {
            return DfaOnChar as Dfa<TChar>;
        }
        throw new NotSupportedException();
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

    internal Group GetGroup(uint index)
    {
        Debug.Assert(index > 0 && index <= GrammarTables.GroupRowCount);
        return new(this, index);
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
    /// <remarks>
    /// Special names are intended to be used on token symbols that will be emitted by custom
    /// tokenizers. Because symbol names are not guaranteed to be unique, a special name
    /// provides a guaranteed way to retrieve the handle for a specific symbol.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="specialName"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">The symbol was not found and <paramref name="throwIfNotFound"/>
    /// had a value of <see langword="true"/>.</exception>
    public EntityHandle GetSymbolFromSpecialName(string specialName, bool throwIfNotFound = false)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(specialName);

        ReadOnlySpan<byte> grammarFile = GrammarFile;
        if (StringHeap.LookupString(grammarFile, specialName.AsSpan()) is { } nameHandle)
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
            ThrowHelpers.ThrowSpecialNameNotFound(specialName);
        }
        return default;
    }

    /// <summary>
    /// Checks whether the given <see cref="TokenSymbolHandle"/> points to a
    /// token symbol with the <see cref="TokenSymbolAttributes.Terminal"/> flag set.
    /// </summary>
    /// <param name="handle">The token symbol handle to check.</param>
    public bool IsTerminal(TokenSymbolHandle handle) => GrammarTables.IsTerminal(handle);

    internal bool IsUnparsable([NotNullWhen(true)] out string? errorResourceKey)
    {
        GrammarAttributes flags = GrammarInfo.Attributes;
        if ((flags & GrammarAttributes.Unparsable) != 0)
        {
            errorResourceKey = nameof(Resources.Parser_UnparsableGrammar);
            return true;
        }
        if (HasUnknownData && (flags & GrammarAttributes.Critical) != 0)
        {
            errorResourceKey = nameof(Resources.Parser_UnparsableGrammar_Critical);
            return true;
        }
        errorResourceKey = null;
        return false;
    }

    Grammar IGrammarProvider.GetGrammar() => this;

    internal void ValidateContent()
    {
        ReadOnlySpan<byte> grammarFile = GrammarFile;

        GrammarTables.ValidateContent(grammarFile, in StringHeap, in BlobHeap);
        LrStateMachine?.ValidateContent(grammarFile, in GrammarTables);
        DfaOnChar?.ValidateContent(grammarFile, in GrammarTables);
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
