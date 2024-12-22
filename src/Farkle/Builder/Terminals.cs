// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
#if NET7_0_OR_GREATER
using System.Numerics;
#else
using System.Diagnostics;
#endif

namespace Farkle.Builder;

/// <summary>
/// Provides factory methods for common terminal symbols.
/// </summary>
public static class Terminals
{
    private static readonly Regex s_unsignedIntRegex = Regex.OneOf(('0', '9')).AtLeast(1);

    private static readonly Regex s_intRegex = Regex.OneOf(('-', '-')).Optional() + s_unsignedIntRegex;

    private static readonly Regex s_unsignedFloatRegex = Regex.Join(
        Regex.OneOf(('-', '-')).Optional(),
        s_unsignedIntRegex,
        Regex.OneOf('.'),
        s_unsignedIntRegex,
        Regex.Join(
            Regex.OneOf('e', 'E'),
            Regex.OneOf('+', '-').Optional(),
            s_unsignedIntRegex
        ).Optional()
    );

    private static readonly Regex s_signedFloatRegex = Regex.OneOf('-', '-').Optional() + s_unsignedFloatRegex;

    // Helper method that returns either a string or a ReadOnlySpan<char> depending on the target framework.
    // This method is intended to be used for the framework's parsing methods.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
    internal static ReadOnlySpan<char> ToCharacters(ReadOnlySpan<char> chars) => chars;
#else
    internal static string ToCharacters(ReadOnlySpan<char> chars) => chars.ToString();
#endif

    private static string TransformString(ref ParserState state, ReadOnlySpan<char> str)
    {
        str = str[1..^1];

        // Fast path if there are no escape sequences.
        if (!str.Contains('\\'))
        {
            return str.ToString();
        }

        var sb = new StringBuilder(str.Length);
        while (!str.IsEmpty)
        {
            switch (str.IndexOf('\\'))
            {
                case -1:
                    sb.Append(str);
                    str = [];
                    break;
                case int backslashIdx:
                    sb.Append(str[..backslashIdx]);
                    char charAtBackslash = str[backslashIdx + 1];
                    char c = charAtBackslash switch
                    {
                        'a' => '\a',
                        'b' => '\b',
                        'e' => '\x1B', // Escape character
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'u' => (char)ushort.Parse(ToCharacters(str.Slice(backslashIdx + 2, 4)), NumberStyles.HexNumber),
                        'v' => '\v',
                        _ => charAtBackslash
                    };
                    sb.Append(c);
                    str = str[(backslashIdx + (charAtBackslash == 'u' ? 6 : 2))..];
                    break;
            }
        }
        return sb.ToString();
    }

#if NET7_0_OR_GREATER
    private static T TransformInteger<T>(ref ParserState state, ReadOnlySpan<char> str) where T : INumberBase<T>
    {
        try
        {
            return T.Parse(str, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new ParserApplicationException(ex.Message);
        }
    }

    private static T TransformFloat<T>(ref ParserState state, ReadOnlySpan<char> str) where T : INumberBase<T>
    {
        try
        {
            return T.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new ParserApplicationException(ex.Message);
        }
    }
#else
    private static T TransformInteger<T>(ref ParserState state, ReadOnlySpan<char> str)
    {
        try
        {
            var characters = ToCharacters(str);
            if (typeof(T) == typeof(int))
            {
                return (T)(object)int.Parse(characters, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            if (typeof(T) == typeof(uint))
            {
                return (T)(object)uint.Parse(characters, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            if (typeof(T) == typeof(long))
            {
                return (T)(object)long.Parse(characters, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)ulong.Parse(characters, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            Debug.Fail("Unknown generic type");
            return default;
        }
        catch (Exception ex)
        {
            throw new ParserApplicationException(ex.Message);
        }
    }

    private static T TransformFloat<T>(ref ParserState state, ReadOnlySpan<char> str)
    {
        try
        {
            var characters = ToCharacters(str);
            if (typeof(T) == typeof(float))
            {
                return (T)(object)float.Parse(characters, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            if (typeof(T) == typeof(double))
            {
                return (T)(object)double.Parse(characters, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)decimal.Parse(characters, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            Debug.Fail("Unknown generic type");
            return default;
        }
        catch (Exception ex)
        {
            throw new ParserApplicationException(ex.Message);
        }
    }
#endif

#if !NET7_0_OR_GREATER
    // We use shared documentation for frameworks both with and without generic math.
    // INumberBase<TSelf> and co. do not exist prior to .NET 7, but on these frameworks the
    // link to this interface will not even be presented, because the public methods are not
    // generic.
#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
#endif
    /// <summary>
    /// Creates a terminal that matches an unsigned integer.
    /// </summary>
    /// <typeparam name="T">The type this terminal returns. Must implement <see cref="INumberBase{TSelf}"/>.</typeparam>
    /// <param name="name">The terminal's name.</param>
    /// <remarks>
    /// This terminal matches non-empty sequences of decimal digits. Leading zeroes are not prohibited.
    /// </remarks>
#if NET7_0_OR_GREATER
    public static IGrammarSymbol<T> UnsignedInteger<T>(string name) where T : INumberBase<T> =>
#else
    private static IGrammarSymbol<T> UnsignedInteger<T>(string name) =>
#endif
        Terminal.Create(name, s_unsignedIntRegex, TransformInteger<T>);

    /// <summary>
    /// Creates a terminal that matches a signed integer.
    /// </summary>
    /// <typeparam name="T">The type this terminal returns. Must implement <see cref="ISignedNumber{TSelf}"/>.</typeparam>
    /// <param name="name">The terminal's name.</param>
    /// <remarks>
    /// This terminal matches non-empty sequences of decimal digits. Leading zeroes are not prohibited.
    /// A leading minus sign is also allowed.
    /// </remarks>
#if NET7_0_OR_GREATER
    public static IGrammarSymbol<T> SignedInteger<T>(string name) where T : ISignedNumber<T> =>
#else
    private static IGrammarSymbol<T> SignedInteger<T>(string name) =>
#endif
        Terminal.Create(name, s_intRegex, TransformInteger<T>);

#if NET7_0_OR_GREATER
    /// <summary>
    /// Creates a terminal that matches an unsigned floating-point number.
    /// </summary>
    /// <typeparam name="T">The type this terminal returns. Must implement <see cref="IFloatingPoint{TSelf}"/>.</typeparam>
    /// <param name="name">The terminal's name.</param>
    /// <remarks>
    /// <para>
    /// This terminal matches non-empty sequences of decimal digits, separated by a dot, and optionally followed by an
    /// exponent of the form <c>[eE][+-]?[0-9]+</c>.
    /// </para>
    /// <para>
    /// The set of accepted strings has changed in Farkle 7. Previously the dot, and digits either only
    /// before or only after it, were optional, which made the terminal match integer literals, but caused
    /// conflicts. Making them mandatory avoids these conflicts and improves simplicity and predictability.
    /// </para>
    /// </remarks>
    public static IGrammarSymbol<T> UnsignedFloat<T>(string name) where T : IFloatingPoint<T> =>
        Terminal.Create(name, s_unsignedFloatRegex, TransformFloat<T>);
#endif

    /// <summary>
    /// Creates a terminal that matches a signed floating-point number.
    /// </summary>
    /// <typeparam name="T">The type this terminal returns. Must implement <see cref="IFloatingPoint{TSelf}"/>.</typeparam>
    /// <param name="name">The terminal's name.</param>
    /// <remarks>
    /// <para>
    /// This terminal matches non-empty sequences of decimal digits, separated by a dot, and optionally followed by an
    /// exponent of the form <c>[eE][+-]?[0-9]+</c>. A leading minus sign is also allowed.
    /// </para>
    /// <para>
    /// The set of accepted strings has changed in Farkle 7. Previously the dot, and digits either only
    /// before or only after it, were optional, which made the terminal match integer literals, but caused
    /// conflicts. Making them mandatory avoids these conflicts and improves simplicity and predictability.
    /// </para>
    /// </remarks>
#if NET7_0_OR_GREATER
    public static IGrammarSymbol<T> SignedFloat<T>(string name) where T : IFloatingPoint<T> =>
#else
    private static IGrammarSymbol<T> SignedFloat<T>(string name) =>
#endif
        Terminal.Create(name, s_signedFloatRegex, TransformFloat<T>);
#if !NET7_0_OR_GREATER
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
#endif

    /// <inheritdoc cref="SignedInteger{T}(string)"/>
    public static IGrammarSymbol<int> Int32(string name) => SignedInteger<int>(name);

    /// <inheritdoc cref="SignedInteger{T}(string)"/>
    public static IGrammarSymbol<long> Int64(string name) => SignedInteger<long>(name);

    /// <inheritdoc cref="UnsignedInteger{T}(string)"/>
    public static IGrammarSymbol<uint> UInt32(string name) => UnsignedInteger<uint>(name);

    /// <inheritdoc cref="UnsignedInteger{T}(string)"/>
    public static IGrammarSymbol<ulong> UInt64(string name) => UnsignedInteger<ulong>(name);

    /// <inheritdoc cref="SignedFloat{T}(string)"/>
    public static IGrammarSymbol<float> Single(string name) => SignedFloat<float>(name);

    /// <inheritdoc cref="SignedFloat{T}(string)"/>
    public static IGrammarSymbol<double> Double(string name) => SignedFloat<double>(name);

    /// <inheritdoc cref="SignedFloat{T}(string)"/>
    public static IGrammarSymbol<decimal> Decimal(string name) => SignedFloat<decimal>(name);

    /// <summary>
    /// Creates a terminal that matches a C-style string.
    /// </summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="delimiter">The character that starts and ends the string.</param>
    /// <param name="escapeChars">The characters that are allowed to be escaped with a <c>\</c>.
    /// <c>\</c> and <paramref name="delimiter"/> are always allowed to be escaped.</param>
    /// <param name="multiLine">Whether new line characters are allowed.</param>
    /// <remarks>
    /// The following escape sequences if enabled in <paramref name="escapeChars"/> have special meaning:
    /// <list type="bullet">
    /// <item>
    /// <c>\a</c>, <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c>, <c>\t</c> and <c>\v</c> correspond to the
    /// same characters as in C.
    /// </item>
    /// <item>
    /// <c>\e</c> corresponds to the <c>U+001B</c> escape character.
    /// </item>
    /// <item>
    /// <c>\UXXXX</c>, where <c>X</c> is a case-insensitive hexadecimal number, corresponds
    /// to the <c>U+XXXX</c> character.
    /// </item>
    /// </list>
    /// Escape sequences with all other characters are treated as the character itself.
    /// </remarks>
    public static IGrammarSymbol<string> String(string name, char delimiter, string escapeChars, bool multiLine)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(name);
        ArgumentNullExceptionCompat.ThrowIfNull(escapeChars);

        bool allowEscapeUnicode = escapeChars.Contains('u');
        var regexDelimiter = Regex.Literal(delimiter);
        var regexBackslash = Regex.Literal('\\');
        var regex =
            Regex.Join(
                regexDelimiter,
                Regex.Choice(
                    Regex.NotOneOf(multiLine ? ['\n', '\r', '\\', delimiter] : ['\\', delimiter]),
                    Regex.Join(
                        regexBackslash,
                        Regex.Choice(
                            regexBackslash,
                            regexDelimiter,
                            Regex.OneOf(escapeChars.ToImmutableArray()),
                            allowEscapeUnicode
                                ? Regex.Join(
                                    Regex.Literal('u'),
                                    Regex.OneOf(('0', '9'), ('a', 'f'), ('A', 'F')).Repeat(4)
                                )
                                : Regex.Empty
                        )
                    )
                ).ZeroOrMore(),
                regexDelimiter
            ).CaseSensitive();

        return Terminal.Create(name, regex, TransformString);
    }

    /// <summary>
    /// Creates a terminal that matches a single-line C-style string.
    /// </summary>
    /// <param name="name">The terminal's name.</param>
    /// <param name="delimiter">The character that starts and ends the string.</param>
    /// <remarks>
    /// All escape sequences described in <see cref="String(string, char, string, bool)"/>
    /// are supported.
    /// </remarks>
    public static IGrammarSymbol<string> String(string name, char delimiter) =>
        String(name, delimiter, "abefnrtvu", false);
}
