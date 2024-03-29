// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Collections;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Grammars.Writers;

internal struct BlobHeapWriter
{
    private List<ImmutableArray<byte>>? _blobs;
    private BlobDictionary<BlobHandle>? _blobHandles;

    public int LengthSoFar { get; private set; }

    [MemberNotNull(nameof(_blobs), nameof(_blobHandles))]
    private void Initialize()
    {
        _blobs ??= new() { ImmutableArray<byte>.Empty };
        _blobHandles ??= new();
        if (LengthSoFar == 0)
        {
            LengthSoFar = 1;
        }
    }

    private static int GetBlobLengthSize(int length, [CallerArgumentExpression(nameof(length))] string? parameterName = null) =>
        (ulong)length switch
        {
            <= 0x7F => 1,
            <= 0x3FFF => 2,
            <= 0x1FFFFFF => 4,
            _ => ThrowHelpers.ThrowBlobTooBig(length, parameterName)
        };

    public BlobHandle Add(PooledSegmentBufferWriter<byte> blob)
    {
        if (blob.TryGetWrittenSpan(out var span))
        {
            return Add(span);
        }

        int blobLength = checked((int)blob.WrittenCount);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(blobLength);
        bool copied = blob.TryCopyTo(buffer);
        Debug.Assert(copied);
        BlobHandle handle = Add(buffer.AsSpan(0, blobLength));
        ArrayPool<byte>.Shared.Return(buffer);
        return handle;
    }

    public BlobHandle Add(ReadOnlySpan<byte> blob)
    {
        if (blob.IsEmpty)
        {
            return default;
        }

        int blobLengthSize = GetBlobLengthSize(blob.Length);

        Initialize();

        if (blob.Length >= GrammarConstants.MaxHeapSize - LengthSoFar - blobLengthSize)
        {
            ThrowHelpers.ThrowOutOfMemoryException($"Blob heap cannot exceed {GrammarConstants.MaxHeapSize} bytes in size.");
        }

        BlobHandle handle = new((uint)LengthSoFar);
        handle = _blobHandles.GetOrAdd(blob, handle, out bool exists, out ImmutableArray<byte> immutableBlob);

        if (!exists)
        {
            LengthSoFar += blobLengthSize + blob.Length;
            _blobs.Add(immutableBlob);
        }

        return handle;
    }

    public void ValidateHandle(BlobHandle handle)
    {
        if (handle.Value < (uint)LengthSoFar)
        {
            return;
        }
        ThrowHelpers.ThrowArgumentException(nameof(handle), "Invalid blob handle.");
    }

    public void WriteTo(IBufferWriter<byte> writer)
    {
        if (_blobs is null)
        {
            return;
        }

        foreach (ImmutableArray<byte> blob in _blobs)
        {
            writer.WriteBlobLength(blob.Length);
            writer.Write(blob.AsSpan());
        }
    }
}
