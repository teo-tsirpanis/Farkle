// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a diagnostic message with no parameters that can be localized.
/// </summary>
internal sealed class LocalizedDiagnostic : IFormattable
{
    private readonly string _resourceKey;

    public LocalizedDiagnostic(string resourceKey)
    {
        _resourceKey = resourceKey;
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Resources.GetResourceString(_resourceKey, formatProvider);

    public override string ToString() =>
        Resources.GetResourceString(_resourceKey);
}
