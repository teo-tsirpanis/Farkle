// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        ranges throughout the builder's pipeline (with the exception of case
        desensitivizing).

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

    private static bool HaveSameFlags(Regex left, Regex right) =>
        (left._kindAndFlags & ~Kind.KindMask) == (right._kindAndFlags & ~Kind.KindMask);

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
            case (Kind.Chars or Kind.AllButChars, (char, char)[]):
            case (Kind.Concat or Kind.Alt, Regex[]):
            case (Kind.Loop, Regex):
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

    internal bool IsAny() => KindReal == Kind.Any;

    internal bool IsStringLiteral([MaybeNullWhen(false)] out string s)
    {
        if (KindReal == Kind.StringLiteral)
        {
            s = (string)_data!;
            return true;
        }
        s = null;
        return false;
    }

    internal bool IsChars(out ImmutableArray<(char, char)> chars, out bool isInverted)
    {
        if (KindReal is Kind.Chars or Kind.AllButChars)
        {
            isInverted = KindReal == Kind.AllButChars;
            chars = ImmutableCollectionsMarshal.AsImmutableArray(((char, char)[])_data!);
            return true;
        }
        chars = [];
        isInverted = false;
        return false;
    }

    internal bool IsConcat(out ImmutableArray<Regex> regexes)
    {
        if (KindReal == Kind.Concat)
        {
            regexes = ImmutableCollectionsMarshal.AsImmutableArray((Regex[])_data!);
            return true;
        }
        regexes = [];
        return false;
    }

    internal bool IsAlt(out ImmutableArray<Regex> regexes)
    {
        if (KindReal == Kind.Alt)
        {
            regexes = ImmutableCollectionsMarshal.AsImmutableArray((Regex[])_data!);
            return true;
        }
        regexes = [];
        return false;
    }

    internal bool IsLoop([MaybeNullWhen(false)] out Regex regex, out int m, out int n)
    {
        m = M;
        n = N;
        if (KindReal == Kind.Loop)
        {
            regex = (Regex)_data!;
            return true;
        }
        regex = null;
        return false;
    }

    internal bool IsRegexString([MaybeNullWhen(false)] out RegexStringHolder regexString)
    {
        if (KindReal == Kind.RegexString)
        {
            regexString = (RegexStringHolder)_data!;
            return true;
        }
        regexString = null;
        return false;
    }

    /// <summary>
    /// Effects the case sensitivity override of this <see cref="Regex"/>, after considering
    /// the state of the DFA builder.
    /// </summary>
    /// <param name="existingIsCaseSensitive">The existing case sensitivity setting at the time the
    /// DFA builder encountered this regex.</param>
    /// <param name="isCaseOverridden">Whether the case sensitivity has been overriden by a parent
    /// regex at the same level. This option allows overriding the case sensitivity of a string
    /// regex. If the parameter's value is <see langword="true"/>, the case sensitivity settings of this
    /// regex will not be considered.</param>
    /// <returns>Whether the regex and its children should be matched as case sensitive.</returns>
    internal bool AdjustCaseSensitivityFlag(bool existingIsCaseSensitive, ref bool isCaseOverridden)
    {
        if (!isCaseOverridden)
        {
            switch (_kindAndFlags & Kind.CaseMask)
            {
                case Kind.CaseSensitive:
                    isCaseOverridden = true;
                    return true;
                case Kind.CaseInsensitive:
                    isCaseOverridden = true;
                    return false;
            }
        }

        return existingIsCaseSensitive;
    }

    /// <summary>
    /// A <see cref="Regex"/> that matches any character.
    /// </summary>
    public static Regex Any { get; } = new(Kind.Any, null);

    /// <summary>
    /// A <see cref="Regex"/> that matches the empty string.
    /// </summary>
    public static Regex Empty { get; } = new(Kind.Concat, (Regex[])[]);

    /// <summary>
    /// A <see cref="Regex"/> that does not match anything.
    /// </summary>
    internal static Regex Void { get; } = new(Kind.Alt, (Regex[])[]);

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

        if (s.Length == 0)
        {
            return Empty;
        }
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
        char[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(chars);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe, nameof(chars));

        if (arrayUnsafe.Length == 0)
        {
            return Any;
        }

        return new(Kind.Chars, arrayUnsafe.Select(c => (c, c)).ToArray());
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
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe, nameof(ranges));
        ValidateCharacterRange(arrayUnsafe.AsSpan());

        if (arrayUnsafe.Length == 0)
        {
            return Any;
        }

        return new(Kind.AllButChars, arrayUnsafe);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches specific characters.
    /// </summary>
    /// <param name="chars">An immutable array with the characters.</param>
    /// <remarks>
    /// Passing an empty array to <paramref name="chars"/> will result in
    /// a regex that cannot match anything. This is usually not desirable
    /// and will result in a build-time warning.
    /// </remarks>
    public static Regex OneOf(ImmutableArray<char> chars)
    {
        char[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(chars);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe, nameof(chars));

        if (arrayUnsafe.Length == 0)
        {
            return Void;
        }

        return new(Kind.Chars, arrayUnsafe.Select(c => (c, c)).ToArray());
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches characters in specific ranges.
    /// </summary>
    /// <param name="ranges">An immutable array with the character ranges,
    /// inclusive.</param>
    /// <exception cref="ArgumentException">A range's start is greater
    /// than its end.</exception>
    /// <remarks>
    /// Passing an empty array to <paramref name="ranges"/> will result in
    /// a regex that cannot match anything. This is usually not desirable
    /// and will result in a build-time warning.
    /// </remarks>
    public static Regex OneOf(ImmutableArray<(char, char)> ranges)
    {
        (char, char)[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(ranges);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe, nameof(ranges));
        ValidateCharacterRange(arrayUnsafe.AsSpan());

        if (arrayUnsafe.Length == 0)
        {
            return Void;
        }

        return new(Kind.Chars, arrayUnsafe);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches many regexes in sequence.
    /// </summary>
    /// <param name="regexes">An immutable array of regexes.</param>
    public static Regex Join(ImmutableArray<Regex> regexes)
    {
        Regex[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(regexes);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe, nameof(regexes));
        foreach (Regex regex in arrayUnsafe)
            ArgumentNullExceptionCompat.ThrowIfNull(regex, nameof(regexes));

        return arrayUnsafe switch
        {
            [] => Empty,
            [var x] => x,
            _ => new(Kind.Concat, arrayUnsafe),
        };
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches either one of many regexes.
    /// </summary>
    /// <param name="regexes">An immutable array of regexes.</param>
    /// <remarks>
    /// Passing an empty array to <paramref name="regexes"/> will result in
    /// a regex that cannot match anything. This is usually not desirable
    /// and will result in a build-time warning.
    /// </remarks>
    public static Regex Choice(ImmutableArray<Regex> regexes)
    {
        Regex[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(regexes);
        ArgumentNullExceptionCompat.ThrowIfNull(arrayUnsafe, nameof(regexes));
        foreach (Regex regex in arrayUnsafe)
            ArgumentNullExceptionCompat.ThrowIfNull(regex, nameof(regexes));

        return arrayUnsafe switch
        {
            [] => Void,
            [var x] => x,
            _ => new(Kind.Alt, arrayUnsafe),
        };
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
    public static Regex operator +(Regex left, Regex right)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(left);
        ArgumentNullExceptionCompat.ThrowIfNull(right);

        // Try to optimize for certain patterns.
        // We can't safely do that if the regexes have different flags.
        if (HaveSameFlags(left, right))
        {
            // Optimize a(bc) to abc.
            // This is important to ensure the depth of the regex tree remains
            // constant when the user combines many regexes with +.
            bool isLeftConcat = left.IsConcat(out var leftConcat);
            bool isRightConcat = right.IsConcat(out var rightConcat);
            switch ((isLeftConcat, isRightConcat))
            {
                case (true, true):
                    return Join([.. leftConcat, .. rightConcat]);
                case (true, false):
                    return Join([.. leftConcat, right]);
                case (false, true):
                    return Join([left, .. rightConcat]);
                case (false, false):
                    break;
            }
            // Optimize ("abc")("def") to "abcdef".
            if (left.IsStringLiteral(out var leftString) &&
                right.IsStringLiteral(out var rightString))
            {
                return Literal(leftString + rightString);
            }
        }
        return Join([left, right]);
    }

    /// <summary>
    /// Combines two <see cref="Regex"/> objects with an OR operator.
    /// </summary>
    /// <param name="left">The first regex.</param>
    /// <param name="right">The second regex.</param>
    /// <returns>A <see cref="Regex"/> that matches either
    /// <paramref name="left"/> or <paramref name="right"/>.</returns>
    /// <seealso cref="Join"/>
    public static Regex operator |(Regex left, Regex right)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(left);
        ArgumentNullExceptionCompat.ThrowIfNull(right);

        if (HaveSameFlags(left, right))
        {
            // Optimize a|(b|c) to a|b|c.
            // This is important to ensure the depth of the regex tree remains
            // constant when the user combines many regexes with |.
            switch ((left.IsAlt(out var leftAlt), right.IsAlt(out var rightAlt)))
            {
                case (true, true):
                    return Choice([.. leftAlt, .. rightAlt]);
                case (true, false):
                    return Choice([.. leftAlt, right]);
                case (false, true):
                    return Choice([left, .. rightAlt]);
                case (false, false):
                    break;
            }
            // Optimize [abc]|[def] to [abcdef].
            // Farkle 6 also optimized patterns with AllButChars, but that would
            // involve intersecting or taking the difference of the two sets, and
            // it's not likely to occur either way.
            if (left.IsChars(out var leftChars, out var leftIsInverted) &&
                right.IsChars(out var rightChars, out var rightIsInverted) &&
                !(leftIsInverted || rightIsInverted))
            {
                return OneOf([.. leftChars, .. rightChars]);
            }
        }
        return Choice([left, right]);
    }

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
