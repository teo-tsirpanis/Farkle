// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Diagnostics;

/// <summary>
/// Contains information about a tokenizer error where input ended inside a group.
/// </summary>
public sealed class UnexpectedEndOfInputInGroupError : IFormattable
#if NET8_0_OR_GREATER
    , ISpanFormattable
#endif
{
    /// <summary>
    /// The name of the group that was left open at the time input ended.
    /// </summary>
    /// <remarks>
    /// In case of nested groups, this property contains the name of the innermost group.
    /// </remarks>
    public string GroupName { get; }

    /// <summary>
    /// Creates a <see cref="UnexpectedEndOfInputInGroupError"/>.
    /// </summary>
    /// <param name="groupName">The value of <paramref name="groupName"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="groupName"/> is <see langword="null"/>.</exception>
    public UnexpectedEndOfInputInGroupError(string groupName)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(groupName);
        GroupName = groupName;
    }

    private string ToString(IFormatProvider? formatProvider) =>
        Resources.Format(formatProvider, nameof(Resources.Parser_UnexpectedEndOfInputInGroup), GroupName);

#if NET8_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        Resources.TryWrite(destination, provider, nameof(Resources.Parser_UnexpectedEndOfInputInGroup), out charsWritten, GroupName);
#endif

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <inheritdoc/>
    public override string ToString() => ToString(null);
}
