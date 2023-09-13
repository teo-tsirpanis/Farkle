// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a diagnostic message with no parameters that can be localized.
/// </summary>
internal sealed class LocalizedDiagnostic(string resourceKey) : IFormattable
{
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        Resources.GetResourceString(resourceKey, formatProvider);

    public override string ToString() =>
        Resources.GetResourceString(resourceKey);
}
