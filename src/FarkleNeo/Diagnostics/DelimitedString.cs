// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text;

namespace Farkle.Diagnostics;

internal readonly struct DelimitedString(ImmutableArray<string?> values, string delimiter,
    string fallback, Func<string, string>? valueTransform = null)
{
    private readonly Func<string, string> ValueTransform = valueTransform ?? (x => x);

    public override string ToString()
    {
        switch (values)
        {
            case []: return string.Empty;
            case [null]: return fallback;
            case [var x]: return ValueTransform(x);
        }

        StringBuilder sb = new();
        bool first = true;
        foreach (string? value in values)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                sb.Append(delimiter);
            }
            sb.Append(value is null ? fallback : ValueTransform(value));
        }
        return sb.ToString();
    }
}
