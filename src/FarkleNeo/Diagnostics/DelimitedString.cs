// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text;

namespace Farkle.Diagnostics;

internal readonly struct DelimitedString
{
    private readonly ImmutableArray<string?> _values;
    private readonly string _delimiter;
    private readonly string _fallback;
    private readonly Func<string, string> _valueTransform;

    public DelimitedString(ImmutableArray<string?> values, string delimiter, string fallback, Func<string, string>? valueTransform = null)
    {
        _values = values;
        _delimiter = delimiter;
        _fallback = fallback;
        _valueTransform = valueTransform ?? (x => x);
    }

    public override string ToString()
    {
        switch (_values)
        {
            case []: return string.Empty;
            case [null]: return _fallback;
            case [var x]: return _valueTransform(x);
        }

        StringBuilder sb = new();
        bool first = true;
        foreach (string? value in _values)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(_delimiter);
            }
            sb.Append(value is null ? _fallback : _valueTransform(value));
        }
        return sb.ToString();
    }
}
