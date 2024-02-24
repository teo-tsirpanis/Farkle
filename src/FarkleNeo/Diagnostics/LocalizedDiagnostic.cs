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

    internal sealed class Composite<TArg>(string resourceKey, TArg arg) : IFormattable
#if NET8_0_OR_GREATER
        , ISpanFormattable
#endif
    {
        public string ResourceKey { get; } = resourceKey;

        public TArg Arg { get; } = arg;

#if NET8_0_OR_GREATER
        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Resources.TryWrite(destination, provider, ResourceKey, out charsWritten, Arg);
#endif

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            Resources.Format(formatProvider, ResourceKey, Arg);

        public override string ToString() =>
            Resources.Format(null, ResourceKey, Arg);
    }

    public static object Create(string resourceKey) => new Simple(resourceKey);

    public static object Create<TArg>(string resourceKey, TArg arg) => new Composite<TArg>(resourceKey, arg);
}
