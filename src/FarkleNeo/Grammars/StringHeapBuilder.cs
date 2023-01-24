// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Farkle.Grammars;

internal struct StringHeapBuilder
{
    private List<string>? _strings;
    private Dictionary<string, StringHandle>? _stringIndices;

    public int LengthSoFar { get; private set; }

    [MemberNotNull(nameof(_strings), nameof(_stringIndices))]
    private void Initialize()
    {
        _strings ??= new List<string>() { "" };
        _stringIndices ??= new Dictionary<string, StringHandle>() { { "", default } };
        if (LengthSoFar == 0)
        {
            LengthSoFar = 1;
        }
    }

    public StringHandle Add(string str)
    {
        if (str is "")
        {
            return default;
        }

        if (str.Contains('\0'))
        {
            ThrowHelpers.ThrowArgumentException(nameof(str), "String cannot contain null characters.");
        }

        Initialize();

        if (_stringIndices.TryGetValue(str, out var index))
        {
            return index;
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

        index = new((uint)LengthSoFar);
        LengthSoFar += stringLength;
        _strings.Add(str);
        _stringIndices.Add(str, index);
        return index;
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
