// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace System.Text;

[ExcludeFromCodeCoverage]
internal static class EncodingExtensionsCompat
{
    private const int MaxInputElementsPerIteration = 1 * 1024 * 1024;

    public static long GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, IBufferWriter<byte> writer)
    {
        if (chars.Length <= MaxInputElementsPerIteration)
        {
            // The input span is small enough where we can one-shot this.

            int byteCount = encoding.GetByteCount(chars);
            Span<byte> scratchBuffer = writer.GetSpan(byteCount);

            int actualBytesWritten = encoding.GetBytes(chars, scratchBuffer);

            writer.Advance(actualBytesWritten);
            return actualBytesWritten;
        }
        else
        {
            // Allocate a stateful Encoder instance and chunk this.

            Convert(encoding.GetEncoder(), chars, writer, flush: true, out long totalBytesWritten, out _);
            return totalBytesWritten;
        }
    }

    public static void Convert(this Encoder encoder, ReadOnlySpan<char> chars, IBufferWriter<byte> writer, bool flush, out long bytesUsed, out bool completed)
    {
        // We need to perform at least one iteration of the loop since the encoder could have internal state.

        long totalBytesWritten = 0;

        do
        {
            // If our remaining input is very large, instead truncate it and tell the encoder
            // that there'll be more data after this call. This truncation is only for the
            // purposes of getting the required byte count. Since the writer may give us a span
            // larger than what we asked for, we'll pass the entirety of the remaining data
            // to the transcoding routine, since it may be able to make progress beyond what
            // was initially computed for the truncated input data.

            int byteCountForThisSlice = (chars.Length <= MaxInputElementsPerIteration)
              ? encoder.GetByteCount(chars, flush)
              : encoder.GetByteCount(chars[..MaxInputElementsPerIteration], flush: false /* this isn't the end of the data */);

            Span<byte> scratchBuffer = writer.GetSpan(byteCountForThisSlice);

            encoder.Convert(chars, scratchBuffer, flush, out int charsUsedJustNow, out int bytesWrittenJustNow, out completed);

            chars = chars[charsUsedJustNow..];
            writer.Advance(bytesWrittenJustNow);
            totalBytesWritten += bytesWrittenJustNow;
        } while (!chars.IsEmpty);

        bytesUsed = totalBytesWritten;
    }
}
#endif
