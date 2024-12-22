// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if (NETCOREAPP || NETSTANDARD2_1_OR_GREATER) && !NET7_0_OR_GREATER
namespace System.IO
{
    internal static class StreamCompat
    {
        public static int ReadAtLeast(this Stream stream, Span<byte> buffer, int minimumLength)
        {
            int totalRead = 0;
            while (totalRead < minimumLength)
            {
                int n = stream.Read(buffer[totalRead..]);
                if (n == 0)
                {
                    break;
                }
                totalRead += n;
            }
            return totalRead;
        }

        public static void ReadExactly(this Stream stream, Span<byte> buffer)
        {
            while (!buffer.IsEmpty)
            {
                int n = stream.Read(buffer);
                if (n == 0)
                {
                    break;
                }
                buffer = buffer[n..];
            }

            if (!buffer.IsEmpty)
            {
                throw new EndOfStreamException();
            }
        }
    }
}
#endif
