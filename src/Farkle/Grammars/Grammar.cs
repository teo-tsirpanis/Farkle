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
public abstract partial class Grammar : IGrammarProvider
{
    internal readonly StringHeap StringHeap;
    internal readonly BlobHeap BlobHeap;
    internal readonly GrammarTables GrammarTables;

    /// <summary>
    /// The grammar file's format major version.
    /// </summary>
    public ushort FormatVersionMajor { get; }

    /// <summary>
    /// The grammar file's format minor version.
    /// </summary>
    public ushort FormatVersionMinor { get; }

    /// <summary>
    /// A read-only buffer to the <see cref="Grammar"/>'s binary data.
    /// </summary>
    public ReadOnlySpan<byte> Data => GrammarFile;

    internal abstract ReadOnlySpan<byte> GrammarFile { get; }

    /// <summary>
    /// Returns an <see cref="ImmutableArray{T}"/> with the grammar's binary data.
    /// Depending on how the grammar was loaded, an existing array might be returned.
    /// </summary>
    internal virtual ImmutableArray<byte> ToImmutableArray() => GrammarFile.ToImmutableArray();

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

    /// <summary>
    /// A collection of the <see cref="Grammar"/>'s <see cref="SpecialNameDefinition"/>s.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used for presentation purposes only.
    /// For maximum performance, parsers are strongly recommended to use
    /// <see cref="IGrammarProvider.GetSymbolFromSpecialName"/>, or one of the
    /// extension methods in <see cref="GrammarExtensions"/> instead.
    /// </remarks>
    /// <seealso cref="IGrammarProvider.GetSymbolFromSpecialName"/>
    /// <seealso cref="GrammarExtensions.GetTokenSymbolFromSpecialName"/>
    /// <seealso cref="GrammarExtensions.GetNonterminalFromSpecialName"/>
    public SpecialNameDefinitionCollection SpecialNameDefinitions => new(this);

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
        FormatVersionMajor = header.VersionMajor;
        FormatVersionMinor = header.VersionMinor;

        GrammarStreams streams = new(grammarFile, header.StreamCount);

        StringHeap = new(grammarFile, streams.StringHeap);
        BlobHeap = new(streams.BlobHeap);
        GrammarTables = new(grammarFile, streams.TableStream, out bool hasUnknownTables);

        GrammarStateMachines stateMachines = new(grammarFile, in BlobHeap, in GrammarTables, out bool hasUnknownStateMachines);
        (DfaOnChar, LrStateMachine) = StateMachineUtilities.GetGrammarStateMachines(this, grammarFile, in stateMachines);

        HasUnknownData = header.HasUnknownData || hasUnknownTables || hasUnknownStateMachines;
    }

    /// <summary>
    /// Creates a <see cref="Grammar"/> from an immutable byte array.
    /// </summary>
    /// <param name="grammarData">An <see cref="ImmutableArray{Byte}"/>
    /// containing the grammar's data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grammarData"/> has its
    /// <see cref="ImmutableArray{Byte}.IsDefault"/> property set to <see langword="true"/>.</exception>
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    public static Grammar Load(ImmutableArray<byte> grammarData)
    {
        if (grammarData.IsDefault)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(grammarData));
        }
        ManagedMemoryGrammar grammar = new ManagedMemoryGrammar(grammarData);
        grammar.ValidateContent();
        return grammar;
    }

    // Internal for benchmarking purposes.
    // It can be made public once a [RequiresUnsafe] attribute is added to the BCL.
    internal static Grammar LoadUnsafe(ImmutableArray<byte> grammarData)
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
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    public static Grammar Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        byte[] data;
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
        // If the file is very big, read only a part of it to make
        // sure it has a valid header, before reading the entire file.
        using (Stream file = File.OpenRead(path))
        {
            if (file.Length > 4096)
            {
                Span<byte> buffer = stackalloc byte[GrammarHeader.MinHeaderDisambiguatorSize];
                file.ReadExactly(buffer);
                GrammarHeader header = GrammarHeader.Read(buffer);
                ValidateHeader(header);
                file.Position = 0;
            }
            data = new byte[file.Length];
            file.ReadExactly(data);
        }
#else
        data = File.ReadAllBytes(path);
#endif
        return Load(ImmutableCollectionsMarshal.AsImmutableArray(data));
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
    public static Grammar ConvertFromGoldParser(Stream grammarFile)
    {
        ArgumentNullException.ThrowIfNull(grammarFile);
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
        return Load(data);
    }

    /// <summary>
    /// Converts a grammar file produced by GOLD Parser into a <see cref="Grammar"/>.
    /// </summary>
    /// <param name="path">The path to the grammar file.</param>
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    /// <remarks>
    /// Both Enhanced Grammar Tables (EGT) and Compiled Grammar Tables (CGT) files are supported.
    /// </remarks>
    public static Grammar ConvertFromGoldParser(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using Stream grammarFile = File.OpenRead(path);
        return ConvertFromGoldParser(grammarFile);
    }

    // It's unlikely that we will expose an API that supports reading both a Farkle and a GOLD Parser grammar
    // for a couple reasons:
    // 1. Reading a GOLD Parser grammar requires converting it to a Farkle grammar, which is both more expensive
    //    and will root all the grammar writer code when trimming.
    // 2. Farkle and GOLD Parser grammars have different access patterns (random vs sequential), so an API that
    //    supports both would not be appropriate. That's why there are no APIs to load the former from a stream
    //    or the latter from an immutable array.
    // 3. There would be few use cases for such API. If you need it, you can implement it in your own code by
    //    reading the first eight bytes of the file to see if it's a Farkle grammar, and try to convert it otherwise.
    //    Such code is available in the CLI tool's sources at CompositePath.fs.

    internal Dfa<TChar>? GetDfa<TChar>()
    {
        if (typeof(TChar) == typeof(char))
        {
            return DfaOnChar as Dfa<TChar>;
        }
        ThrowHelpers.ThrowUnsupportedCharacterException();
        return null;
    }

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
    /// Gets the <see cref="Group"/> pointed by the given <see cref="GroupHandle"/>.
    /// </summary>
    /// <param name="handle">A handle to the group.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/>'s
    /// <see cref="GroupHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="handle"/>
    /// points to a group that does not exist.</exception>
    public Group GetGroup(GroupHandle handle)
    {
        if (!handle.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(handle));
        }

        if (handle.Value >= GrammarTables.GroupRowCount)
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
    /// Gets a boxed object representing the entity pointed to by the given <see cref="EntityHandle"/>.
    /// </summary>
    /// <param name="handle">A handle to the entity.</param>
    /// <remarks>
    /// This method must not be called in performance-critical code.
    /// </remarks>
    internal object? GetEntity(EntityHandle handle)
    {
        if (handle.IsTokenSymbol)
        {
            return GetTokenSymbol((TokenSymbolHandle)handle);
        }
        if (handle.IsGroup)
        {
            return GetGroup((GroupHandle)handle);
        }
        if (handle.IsNonterminal)
        {
            return GetNonterminal((NonterminalHandle)handle);
        }
        if (handle.IsProduction)
        {
            return GetProduction((ProductionHandle)handle);
        }

        Debug.Assert(!handle.HasValue);
        return null;
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
        ArgumentNullException.ThrowIfNull(specialName);

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
        if (FormatVersionMajor == GrammarConstants.VersionMajor && FormatVersionMinor > GrammarConstants.VersionMinor)
        {
            errorResourceKey = nameof(Resources.Parser_UnparsableGrammar_TooNewFormat);
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

    private sealed class ManagedMemoryGrammar(ImmutableArray<byte> grammarFile) : Grammar(grammarFile.AsSpan())
    {
        private readonly ImmutableArray<byte> _grammarFile = grammarFile;

        internal override ReadOnlySpan<byte> GrammarFile
        {
            get
            {
                // During construction the `GrammarFile` property has not yet been assigned. This assert makes sure that it is not accessed.
                Debug.Assert(!_grammarFile.IsDefault);
                return _grammarFile.AsSpan();
            }
        }

        internal override ImmutableArray<byte> ToImmutableArray() => _grammarFile;
    }

    internal unsafe sealed class PrecompiledGrammar(byte* data, int length, RuntimeTypeHandle keepAlive) : Grammar(new ReadOnlySpan<byte>(data, length))
    {
        // data points to an assembly's RVA field.
        // Keep type alive to prevent unloading it.
        private readonly RuntimeTypeHandle _keepAlive = keepAlive;

        internal override ReadOnlySpan<byte> GrammarFile => new(data, length);
    }
}
