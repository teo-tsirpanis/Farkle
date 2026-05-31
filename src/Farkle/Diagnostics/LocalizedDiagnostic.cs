// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Represents a diagnostic message with no parameters that can be localized.
/// </summary>
internal static class LocalizedDiagnostic
{
    private sealed class Simple(string resourceKey)
    {
        public override string ToString() =>
            Resources.GetResourceString(resourceKey);
    }

    internal sealed class Composite<TArg>(string resourceKey, TArg arg) : ISpanFormattable
    {
        public string ResourceKey { get; } = resourceKey;

        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Resources.TryWrite(destination, provider, ResourceKey, out charsWritten, arg);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            Resources.Format(formatProvider, ResourceKey, arg);

        public override string ToString() => ToString(null, null);
    }

    internal sealed class Composite<TArg1, TArg2>(string resourceKey, TArg1 arg1, TArg2 arg2) : ISpanFormattable
    {
        public string ResourceKey { get; } = resourceKey;

        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Resources.TryWrite(destination, provider, ResourceKey, out charsWritten, arg1, arg2);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            Resources.Format(formatProvider, ResourceKey, arg1, arg2);

        public override string ToString() => ToString(null, null);
    }

    internal sealed class Composite<TArg1, TArg2, TArg3>(string resourceKey, TArg1 arg1, TArg2 arg2, TArg3 arg3) : ISpanFormattable
    {
        public string ResourceKey { get; } = resourceKey;

        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Resources.TryWrite(destination, provider, ResourceKey, out charsWritten, arg1, arg2, arg3);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            Resources.Format(formatProvider, ResourceKey, arg1, arg2, arg3);

        public override string ToString() => ToString(null, null);
    }

    internal sealed class Composite(string resourceKey, object[] args) : ISpanFormattable
    {
        public string ResourceKey { get; } = resourceKey;

        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Resources.TryWrite(destination, provider, ResourceKey, out charsWritten, args);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            Resources.Format(formatProvider, ResourceKey, args);

        public override string ToString() => ToString(null, null);
    }

    public static object Create(string resourceKey) => new Simple(resourceKey);

    public static object Create<TArg>(string resourceKey, TArg arg) => new Composite<TArg>(resourceKey, arg);

    public static object Create<TArg1, TArg2>(string resourceKey, TArg1 arg1, TArg2 arg2) => new Composite<TArg1, TArg2>(resourceKey, arg1, arg2);

    public static object Create<TArg1, TArg2, TArg3>(string resourceKey, TArg1 arg1, TArg2 arg2, TArg3 arg3) => new Composite<TArg1, TArg2, TArg3>(resourceKey, arg1, arg2, arg3);

    public static object Create(string resourceKey, params ReadOnlySpan<object> args) => new Composite(resourceKey, args.ToArray());
}
