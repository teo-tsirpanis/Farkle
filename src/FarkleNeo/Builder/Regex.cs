// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Farkle.Builder;

/// <summary>
/// Represents a pattern that must be matched by terminals in a grammar.
/// </summary>
public sealed class Regex
{
    /*
    The design of this class differs from earlier versions of Farkle in two major ways:

    1.  Embracing ranges: Farkle has always followed GOLD Parser 5's legacy of
        representing character sets as a list of ranges. However, the regexes
        and the DFA builder were representing character sets as trees of
        individual characters, and the ranges were constructed at the end of
        the DFA building process. Farkle 7 will represent character sets as
        ranges throughout the builder's pipeline.

    2.  Reducing upfront computations: In previous versions of Farkle, a regex
        like a{3,} would be expanded to aaaa* at construction time (or worse,
        "abcde" to [a][b][c][d][e]). Now the user-facing Regex type will
        support natively representing more complex constructs, and the expansion
        will happen when and if a DFA is built. This speeds-up initialization
        when the precompiler is being used.
    */

    private readonly Kind _kindAndFlags;

    private readonly object? _data;

    private Kind KindReal => _kindAndFlags & Kind.KindMask;

    private int M { get; }

    private int N { get; }

    private static void ValidateCharacterRange(ReadOnlySpan<(char, char)> ranges)
    {
        foreach ((char start, char end) in ranges)
        {
            if (start > end)
            {
                throw new ArgumentException(Resources.Builder_RegexCharacterRangeReverseOrder, nameof(ranges));
            }
        }
    }

    private Regex(Kind kind, object? data, int m = 1, int n = 1)
    {
        _kindAndFlags = kind;
        _data = data;
        M = m;
        N = n;
        Debug.Assert((_kindAndFlags & Kind.CaseMask) != Kind.CaseMask);
        switch (KindReal, _data)
        {
            case (Kind.Any, null):
            case (Kind.StringLiteral, string):
            case (Kind.Chars | Kind.AllButChars, (char, char)[]):
            case (Kind.Concat | Kind.Alt, Regex[]):
            case (Kind.RegexString, RegexStringHolder):
                break;
            default:
                Debug.Fail("Invalid regex data.");
                break;
        }
        Debug.Assert(M >= 0);
        Debug.Assert(N >= M);
    }

    private Regex Loop(int m, int n)
    {
        if (KindReal == Kind.Loop && m == M && n == N)
            return this;
        return new(Kind.Loop, this, m, n);
    }

    /// <summary>
    /// A <see cref="Regex"/> that matches any character.
    /// </summary>
    public static Regex Any { get; } = new(Kind.Any, null);

    /// <summary>
    /// A <see cref="Regex"/> that matches the empty string.
    /// </summary>
    public static Regex Empty { get; } = Join([]);

    /// <summary>
    /// A <see cref="Regex"/> that matches a specific character.
    /// </summary>
    public static Regex Literal(char c) => Literal(c.ToString());

    /// <summary>
    /// A <see cref="Regex"/> that matches a specific string of characters.
    /// </summary>
    public static Regex Literal(string s)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(s);
        return new(Kind.StringLiteral, s);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> specified by a string pattern.
    /// </summary>
    /// <param name="pattern">The regex's pattern.</param>
    /// <remarks>
    /// This method will not fail if the pattern is invalid, but when
    /// the returned <see cref="Regex"/> is used to build a grammar,
    /// it will result in a build error.
    /// </remarks>
    public static Regex FromRegexString(string pattern)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(pattern);
        return new(Kind.RegexString, RegexStringHolder.Create(pattern));
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches all characters except
    /// of specific ones.
    /// </summary>
    /// <param name="chars">An immutable array with characters.</param>
    public static Regex NotOneOf(ImmutableArray<char> chars)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(chars);
        return new(Kind.AllButChars, chars.Select(c => (c, c)).ToArray());
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches all characters except
    /// of those in specific ranges.
    /// </summary>
    /// <param name="ranges">An immutable array with the character ranges,
    /// inclusive.</param>
    /// <exception cref="ArgumentException">A range's start is greater
    /// than its end.</exception>
    public static Regex NotOneOf(ImmutableArray<(char, char)> ranges)
    {
        (char, char)[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(ranges);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe);
        ValidateCharacterRange(arrayUnsafe.AsSpan());
        return new(Kind.AllButChars, arrayUnsafe);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches specific characters.
    /// </summary>
    /// <param name="chars">An immutable array with the characters.</param>
    public static Regex OneOf(ImmutableArray<char> chars)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(chars);
        return new(Kind.Chars, chars.Select(c => (c, c)).ToArray());
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches characters in specific ranges.
    /// </summary>
    /// <param name="ranges">An immutable array with the character ranges,
    /// inclusive.</param>
    /// <exception cref="ArgumentException">A range's start is greater
    /// than its end.</exception>
    public static Regex OneOf(ImmutableArray<(char, char)> ranges)
    {
        (char, char)[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(ranges);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe);
        ValidateCharacterRange(arrayUnsafe.AsSpan());
        return new(Kind.Chars, arrayUnsafe);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches many regexes in sequence.
    /// </summary>
    /// <param name="regexes">An immutable array of regexes.</param>
    public static Regex Join(ImmutableArray<Regex> regexes)
    {
        Regex[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(regexes);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe);
        foreach (Regex regex in arrayUnsafe)
            ArgumentNullExceptionCompat.ThrowIfNull(regex, nameof(regexes));
        return new(Kind.Concat, arrayUnsafe);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches either one of many regexes.
    /// </summary>
    /// <param name="regexes">An immutable array of regexes.</param>
    public static Regex Choice(ImmutableArray<Regex> regexes)
    {
        Regex[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(regexes);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe);
        foreach (Regex regex in arrayUnsafe)
            ArgumentNullExceptionCompat.ThrowIfNull(regex, nameof(regexes));
        return new(Kind.Alt, arrayUnsafe);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex any number of
    /// times or not at all.
    /// </summary>
    /// <remarks>
    /// This is also known as the Kleene star.
    /// </remarks>
    public Regex ZeroOrMore() => AtLeast(0);

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex a specific number
    /// of times.
    /// </summary>
    /// <param name="n">The number of times to repeat the regex.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/>
    /// is negative, or equal to <see cref="int.MaxValue"/>.</exception>
    public Regex Repeat(int n)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(n);
        return N switch
        {
            0 => Empty,
            1 => this,
            _ => Between(n, n)
        };
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex either once or
    /// not at all.
    /// </summary>
    public Regex Optional() => this is { KindReal: Kind.Loop, M: 0 } ? this : Loop(0, 1);

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex a number of times
    /// within a range.
    /// </summary>
    /// <param name="m">The minimum number of times to repeat the regex, inclusive.</param>
    /// <param name="n">The maximum number of times to repeat the regex, inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="m"/> or
    /// <paramref name="n"/> is negative, <paramref name="m"/> is greater than
    /// <paramref name="n"/>, or <paramref name="n"/> is equal to
    /// <see cref="int.MaxValue"/>.</exception>
    public Regex Between(int m, int n)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(m);
        if (m > n)
        {
            throw new ArgumentException(Resources.Builder_RegexLoopRangeReverseOrder);
        }
        if (n == int.MaxValue)
        {
            throw new ArgumentException(Resources.Builder_RegexLoopMaxTooBig);
        }

        return Loop(m, n);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex at least a specific
    /// number of times.
    /// </summary>
    /// <param name="m">The minimum number of times to repeat the regex, inclusive.</param>
    public Regex AtLeast(int m)
    {
        ArgumentOutOfRangeExceptionCompat.ThrowIfNegative(m);
        return Loop(m, int.MaxValue);
    }

    private Regex WithCase(Kind @case)
    {
        Debug.Assert(@case is Kind.CaseSensitive or Kind.CaseInsensitive);
        if ((_kindAndFlags & Kind.CaseMask) == @case)
            return this;
        return new(_kindAndFlags & ~Kind.CaseMask | @case, _data, M, N);
    }

    /// <summary>
    /// Creates a case-sensitive copy of this <see cref="Regex"/>.
    /// </summary>
    public Regex CaseSensitive() => WithCase(Kind.CaseSensitive);

    /// <summary>
    /// Creates a case-insensitive copy of this <see cref="Regex"/>.
    /// </summary>
    public Regex CaseInsensitive() => WithCase(Kind.CaseInsensitive);

    /// <summary>
    /// Concatenates two <see cref="Regex"/> objects.
    /// </summary>
    /// <param name="left">The first regex.</param>
    /// <param name="right">The second regex.</param>
    /// <returns>A <see cref="Regex"/> that matches <paramref name="left"/>
    /// and then <paramref name="right"/> in sequence.</returns>
    /// <seealso cref="Join"/>
    public static Regex operator +(Regex left, Regex right) => Join([left, right]);

    /// <summary>
    /// Combines two <see cref="Regex"/> objects with an OR operator.
    /// </summary>
    /// <param name="left">The first regex.</param>
    /// <param name="right">The second regex.</param>
    /// <returns>A <see cref="Regex"/> that matches either
    /// <paramref name="left"/> or <paramref name="right"/>.</returns>
    /// <seealso cref="Join"/>
    public static Regex operator |(Regex left, Regex right) => Choice([left, right]);

    [Flags]
    internal enum Kind : byte
    {
        /// <summary>
        /// The regex matches any character.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be <see langword="null"/>.
        /// </remarks>
        Any = 0,
        /// <summary>
        /// The regex matches a string literal.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be a <see cref="string"/>.
        /// </remarks>
        StringLiteral = 1,
        /// <summary>
        /// The regex matches a list of character ranges.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be an array of value 2-tuples of <see cref="char"/>.
        /// </remarks>
        Chars = 2,
        /// <summary>
        /// The regex matches any character except those in a list of character ranges.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be an array of value 2-tuples of <see cref="char"/>.
        /// </remarks>
        AllButChars = 3,
        /// <summary>
        /// The regex matches a concatenation of other regexes.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be an array of <see cref="Regex"/>.
        /// </remarks>
        Concat = 4,
        /// <summary>
        /// The regex matches an alternation of other regexes.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be an array of <see cref="Regex"/>.
        /// </remarks>
        Alt = 5,
        /// <summary>
        /// The regex matches a loop of another regex.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be a <see cref="Regex"/>.
        /// The values of <see cref="M"/> and <see cref="N"/> contain
        /// the minimum and maximum number of repetitions.
        /// </remarks>
        Loop = 6,
        /// <summary>
        /// The regex has a string regex that gets parsed at build time.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be a <see cref="RegexStringHolder"/>.
        /// </remarks>
        RegexString = 7,
        /// <summary>
        /// A mask for the regex kind bits.
        /// </summary>
        KindMask = 0x0F,
        /// <summary>
        /// The regex is forced to be case-sensitive.
        /// </summary>
        CaseSensitive = 0x40,
        /// <summary>
        /// The regex is forced to be case-insensitive.
        /// </summary>
        CaseInsensitive = 0x80,
        /// <summary>
        /// A mask for the case-sensitivity bits.
        /// </summary>
        CaseMask = 0xC0
    }
}
