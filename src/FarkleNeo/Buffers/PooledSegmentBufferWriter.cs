// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Farkle.Buffers;

// TODO: Write unit tests.
internal sealed class PooledSegmentBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    private const int DefaultSegmentCapacity =
#if DEBUG
        1;
#else
        4096;
#endif

    private const int InitialSegmentArraySize = 2;

    private Segment[]? _segments;

    public long WrittenCount { get; private set; }

    private int _activeSegmentIndex;

    private static Segment[] InitializeSegments(int sizeHint)
    {
        Segment[] segments = new Segment[InitialSegmentArraySize];
        segments[0] = new Segment(sizeHint);
        return segments;
    }

    private ref Segment AllocateNewSegment(int sizeHint)
    {
        Debug.Assert(_segments is not (null or []));
        if (_activeSegmentIndex == _segments.Length - 1)
        {
            Array.Resize(ref _segments, _segments.Length * 2);
        }

        ref Segment segment = ref _segments[++_activeSegmentIndex];
        segment = new Segment(sizeHint);
        return ref segment;
    }

    private ref Segment GetOrAllocateSegment(int sizeHint)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(sizeHint);
        Segment[] segments = _segments ??= InitializeSegments(sizeHint);
        ref Segment segment = ref segments[_activeSegmentIndex];
        int remaining = segment.Remaining;
        if (remaining == 0 || (remaining > sizeHint && !segment.TryResizeInPlace(sizeHint)))
        {
            segment = ref AllocateNewSegment(sizeHint);
        }

        return ref segment;
    }

    private ref Segment GetActiveSegmentOrNullRef()
    {
        if (_segments is Segment[] segments)
        {
            return ref segments[_activeSegmentIndex];
        }
        return ref Unsafe.NullRef<Segment>();
    }

    public PooledSegmentBufferWriter() : this(DefaultSegmentCapacity) { }

    public PooledSegmentBufferWriter(int initialCapacity)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(initialCapacity);
        if (initialCapacity > 0)
        {
            _segments = InitializeSegments(initialCapacity);
        }
    }

    public void Dispose()
    {
        foreach (ref Segment segment in _segments.AsSpan())
        {
            segment.Dispose();
        }
        _segments = null;
        WrittenCount = 0;
    }

    public void Clear()
    {
        foreach (ref Segment segment in _segments.AsSpan())
        {
            segment.Clear();
        }
        WrittenCount = 0;
    }

    public void Advance(int count)
    {
        ref Segment activeSegment = ref GetActiveSegmentOrNullRef();
        if (Unsafe.IsNullRef(ref activeSegment))
        {
            ThrowCannotAdvance(count);
        }
        activeSegment.Advance(count);
        WrittenCount += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        Memory<T> memory = GetOrAllocateSegment(sizeHint).RemainingMemory;
        Debug.Assert(memory.Length >= Math.Max(1, sizeHint));
        return memory;
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        Span<T> span = GetOrAllocateSegment(sizeHint).RemainingSpan;
        Debug.Assert(span.Length >= Math.Max(1, sizeHint));
        return span;
    }

    public T[] ToArray()
    {
        if (WrittenCount == 0)
        {
            return Array.Empty<T>();
        }

        int length = (int)WrittenCount;
        if (length != WrittenCount)
        {
            ThrowHelpers.ThrowOutOfMemoryException();
        }

        Segment[]? segments = _segments;
        Debug.Assert(segments is not null);
        T[] result = new T[length];
        int writtenElements = 0;
        for (int i = 0; i < _activeSegmentIndex; i++)
        {
            ReadOnlySpan<T> buffer = segments[i].WrittenSpan;
            buffer.CopyTo(result.AsSpan(writtenElements));
            writtenElements += buffer.Length;
        }
        Debug.Assert(writtenElements == result.Length);
        return result;
    }

    public void WriteTo<TBufferWriter>(ref TBufferWriter bufferWriter) where TBufferWriter : IBufferWriter<T>
    {
        Segment[]? segments = _segments;
        if (segments is null)
        {
            return;
        }

        for (int i = 0; i < _activeSegmentIndex; i++)
        {
            bufferWriter.Write(segments[i].WrittenSpan);
        }
    }

    [DoesNotReturn, StackTraceHidden]
    private static void ThrowCannotAdvance(int count) =>
        throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot advance past the end of the buffer.");

    private struct Segment : IDisposable
    {
        public int WrittenCount { get; private set; }

        public T[]? Buffer;

        private static T[] RentBuffer(int sizeHint) =>
            ArrayPool<T>.Shared.Rent(sizeHint <= 0 ? DefaultSegmentCapacity : sizeHint);

        public readonly int Remaining
        {
            get
            {
                Debug.Assert(Buffer is not null);
                return Buffer.Length - WrittenCount;
            }
        }

        public Segment(int sizeHint)
        {
            Buffer = RentBuffer(sizeHint);
        }

        public void Dispose()
        {
            if (Buffer is null)
            {
                return;
            }
            ArrayPool<T>.Shared.Return(Buffer);
            Buffer = null;
            WrittenCount = 0;
        }

        public void Clear()
        {
            if (Buffer is null)
            {
                return;
            }

#if !NETSTANDARD2_0
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
#endif
            {
                Array.Clear(Buffer, 0, Buffer.Length);
            }
            WrittenCount = 0;
        }

        public bool TryResizeInPlace(int sizeHint)
        {
            // If a segment has not any elements written into it,
            // we can resize it instead of allocating a new one.
            if (WrittenCount != 0)
            {
                return false;
            }

            if (Buffer is not null)
            {
                ArrayPool<T>.Shared.Return(Buffer);
            }
            Buffer = RentBuffer(sizeHint);
            return true;
        }

        public readonly Memory<T> RemainingMemory => Buffer.AsMemory(WrittenCount);

        public readonly Span<T> RemainingSpan => Buffer.AsSpan(WrittenCount);

        public readonly Span<T> WrittenSpan => Buffer.AsSpan(0, WrittenCount);

        public void Advance(int count)
        {
            if ((uint)count > (uint)Remaining)
            {
                ThrowCannotAdvance(count);
            }
            WrittenCount += count;
        }
    }
}
