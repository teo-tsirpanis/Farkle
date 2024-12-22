// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Collections;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Farkle.Grammars.Writers;

internal struct StringHeapWriter
{
    private List<string>? _strings;
    private StringDictionary<StringHandle>? _stringHandles;

    public int LengthSoFar { get; private set; }

    [MemberNotNull(nameof(_strings), nameof(_stringHandles))]
    private void Initialize()
    {
        _strings ??= new() { "" };
        _stringHandles ??= new();
        if (LengthSoFar == 0)
        {
            LengthSoFar = 1;
        }
    }

    public StringHandle Add(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return default;
        }

        if (str.IndexOf('\0') != -1)
        {
            ThrowHelpers.ThrowArgumentException(nameof(str), "String cannot contain null characters.");
        }

        Initialize();

        // We will lookup twice in the dictionary; this way we
        // avoid counting the bytes if the string exists.
        if (_stringHandles.TryGetValue(str.AsSpan(), out var handle))
        {
            return handle;
        }

        int stringLength;
        try
        {
            stringLength = Utf8EncodingStrict.Instance.GetByteCount(str) + 1;
        }
        catch (Exception e)
        {
            ThrowHelpers.ThrowArgumentException(nameof(str), "String contains invalid data", e);
            return default;
        }

        if (stringLength >= GrammarConstants.MaxHeapSize - LengthSoFar)
        {
            ThrowHelpers.ThrowOutOfMemoryException($"String heap cannot exceed {GrammarConstants.MaxHeapSize} bytes in size.");
        }

        handle = new((uint)LengthSoFar);
        handle = _stringHandles.GetOrAdd(str, handle, out bool exists);
        Debug.Assert(!exists);

        LengthSoFar += stringLength;
        _strings.Add(str);
        return handle;
    }

    public void ValidateHandle(StringHandle handle)
    {
        if (handle.Value < (uint)LengthSoFar)
        {
            return;
        }
        ThrowHelpers.ThrowArgumentException(nameof(handle), "String handle is invalid.");
    }

    public void WriteTo(IBufferWriter<byte> writer)
    {
        if (_strings is null)
        {
            return;
        }

        foreach (var str in _strings)
        {
            Utf8EncodingStrict.Instance.GetBytes(str.AsSpan(), writer);
            writer.Write((byte)0);
        }
    }
}
