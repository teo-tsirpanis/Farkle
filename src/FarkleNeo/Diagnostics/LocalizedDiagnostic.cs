// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a diagnostic message with no parameters that can be localized.
/// </summary>
internal static class LocalizedDiagnostic
{
    private sealed class Simple(string resourceKey) : IFormattable
    // It does not have to implement ISpanFormattable because its message
    // is a static string.
    {
        string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
            Resources.GetResourceString(resourceKey, formatProvider);

        public override string ToString() =>
            Resources.GetResourceString(resourceKey);
    }

    public static object Create(string resourceKey) => new Simple(resourceKey);
}
