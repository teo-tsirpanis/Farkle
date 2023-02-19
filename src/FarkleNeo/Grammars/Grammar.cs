// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Collections.Immutable;

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
    private readonly StringHeap _stringHeap;
    private readonly BlobHeap _blobHeap;
    private readonly GrammarTables _grammarTables;

    private protected abstract ReadOnlySpan<byte> GrammarFile { get; }

    /// <summary>
    /// The length of the grammar's data in bytes.
    /// </summary>
    public int DataLength => GrammarFile.Length;

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
            GrammarFileType.Egt5 => "EGT grammar files produced by GOLD Parser 5.x must be opened with the Grammar.CreateFromEgt5 method.",
            GrammarFileType.Cgt => "CGT grammar files produced by GOLD Parser are not supported.",
            _ => "Unrecognized file format."
        };
        ThrowHelpers.ThrowNotSupportedException(errorMessage);
    }

    private protected Grammar(ReadOnlySpan<byte> grammarFile)
    {
        GrammarHeader header = GrammarHeader.Read(grammarFile);
        ValidateHeader(header);

        GrammarStreams streams = new(grammarFile, header.StreamCount, out _);

        _stringHeap = new StringHeap(grammarFile, streams.StringHeapOffset, streams.StringHeapLength);
        _blobHeap = new BlobHeap(streams.BlobHeapOffset, streams.BlobHeapLength);
        _grammarTables = new GrammarTables(grammarFile, streams.TableStreamOffset, streams.TableStreamLength, out _);
    }

    /// <summary>
    /// Creates a <see cref="Grammar"/> from a byte buffer.
    /// </summary>
    /// <param name="grammarData">A <see cref="ReadOnlySpan{Byte}"/>
    /// containing the grammar's data.</param>
    /// <exception cref="NotSupportedException">The data format is unsupported.</exception>
    /// <exception cref="InvalidDataException">The grammar contains invalid data.</exception>
    public static Grammar Create(ReadOnlySpan<byte> grammarData)
    {
        return new ManagedMemoryGrammar(grammarData);
    }

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

        private protected override ReadOnlySpan<byte> GrammarFile =>
            _grammarFile.AsSpan();

        public ManagedMemoryGrammar(ReadOnlySpan<byte> grammarFile) : base(grammarFile)
        {
            // To support in the future unchecked indexing into the file,
            // we must load the grammar in memory we own.
            _grammarFile = grammarFile.ToImmutableArray();
        }
    }
}
