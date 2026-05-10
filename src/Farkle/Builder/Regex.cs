// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Farkle.Buffers;
using Farkle.Grammars;

namespace Farkle.Builder;

/// <summary>
/// Represents a pattern that must be matched by terminals in a grammar.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay(),nq}")]
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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly KindAndFlags _kindAndFlags;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    private readonly object? _data;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private KindAndFlags Kind => _kindAndFlags & KindAndFlags.KindMask;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int M { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
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
        (left._kindAndFlags & ~KindAndFlags.KindMask) == (right._kindAndFlags & ~KindAndFlags.KindMask);

    private Regex(KindAndFlags kind, object? data, int m = 1, int n = 1)
    {
        _kindAndFlags = kind;
        _data = data;
        M = m;
        N = n;
        Debug.Assert((_kindAndFlags & KindAndFlags.CaseMask) != KindAndFlags.CaseMask);
        switch (Kind, _data)
        {
            case (KindAndFlags.Any, null):
            case (KindAndFlags.StringLiteral, string):
            case (KindAndFlags.Chars, char[] or string):
            case (KindAndFlags.CharRanges, (char, char)[]):
            case (KindAndFlags.Concat or KindAndFlags.Alt, Regex[]):
            case (KindAndFlags.RegexString, RegexStringHolder):
            case (KindAndFlags.Accept, Regex):
                break;
            case (KindAndFlags.Loop, Regex):
                Debug.Assert(M >= 0);
                Debug.Assert(N >= M);
                break;
            default:
                Debug.Fail("Invalid regex data.");
                break;
        }
    }

    [ExcludeFromCodeCoverage]
    private string DebuggerDisplay()
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        string dataString = Kind switch
        {
            KindAndFlags.Any =>
                "Any",
            KindAndFlags.StringLiteral =>
                $"\"{_data}\"",
            KindAndFlags.Chars =>
                $"Chars[{new ImmutableBuffer<char>(_data).Length}]",
            KindAndFlags.CharRanges =>
                $"Chars[{(((char, char)[])_data!).Length}]",
            KindAndFlags.Concat =>
                $"Concat[{((Regex[])_data!).Length}]",
            KindAndFlags.Alt =>
                $"Alt[{((Regex[])_data!).Length}]",
            KindAndFlags.Loop =>
                $"{((Regex)_data!).DebuggerDisplay()}{{{M},{N}}}",
            KindAndFlags.RegexString =>
                $"\"{_data}\"",
            KindAndFlags.Accept =>
                $"Accept #{M}{(N != 0 ? " (Lowest Priority)" : "")} {((Regex)_data!).DebuggerDisplay()}",
            _ => ""
        };
        return $"{dataString}{FormatFlags(_kindAndFlags)}";

        static string FormatFlags(KindAndFlags flags)
        {
            flags &= KindAndFlags.FlagsMask;
            List<string> strings = [];
            switch (flags & KindAndFlags.CaseMask)
            {
                case KindAndFlags.CaseSensitive:
                    strings.Add(nameof(KindAndFlags.CaseSensitive));
                    break;
                case KindAndFlags.CaseInsensitive:
                    strings.Add(nameof(KindAndFlags.CaseInsensitive));
                    break;
            }
            switch (flags & KindAndFlags.HighPriorityInverted)
            {
                case KindAndFlags.HighPriorityInverted:
                    strings.Add(nameof(KindAndFlags.HighPriorityInverted));
                    break;
                case KindAndFlags.Inverted:
                    strings.Add(nameof(KindAndFlags.Inverted));
                    break;
            }
            if ((flags & KindAndFlags.BreakOnAccept) != 0)
            {
                strings.Add(nameof(KindAndFlags.BreakOnAccept));
            }

            return strings is [] ? "" : $" ({string.Join(", ", strings)})";
        }
    }

    private Regex Loop(int m, int n)
    {
        if (Kind == KindAndFlags.Loop && m == M && n == N)
            return this;
        return new(KindAndFlags.Loop, this, m, n);
    }

    internal bool IsAny() => Kind == KindAndFlags.Any;

    internal bool IsStringLiteral([MaybeNullWhen(false)] out string s)
    {
        if (Kind == KindAndFlags.StringLiteral)
        {
            s = (string)_data!;
            return true;
        }
        s = null;
        return false;
    }

    internal bool IsChars(out ImmutableBuffer<char> chars, out CharsFlags flags)
    {
        flags = (CharsFlags)_kindAndFlags & CharsFlags.All;
        if (Kind is KindAndFlags.Chars)
        {
            chars = new(_data);
            return true;
        }
        chars = [];
        return false;
    }

    internal bool IsCharRanges(out ImmutableArray<(char, char)> chars, out CharsFlags flags)
    {
        flags = (CharsFlags)_kindAndFlags & CharsFlags.All;
        if (Kind is KindAndFlags.CharRanges)
        {
            chars = ImmutableCollectionsMarshal.AsImmutableArray(((char, char)[])_data!);
            return true;
        }
        chars = [];
        return false;
    }

    internal bool IsConcat(out ImmutableArray<Regex> regexes)
    {
        if (Kind == KindAndFlags.Concat)
        {
            regexes = ImmutableCollectionsMarshal.AsImmutableArray((Regex[])_data!);
            return true;
        }
        regexes = [];
        return false;
    }

    internal bool IsAlt(out ImmutableArray<Regex> regexes)
    {
        if (Kind == KindAndFlags.Alt)
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
        if (Kind == KindAndFlags.Loop)
        {
            regex = (Regex)_data!;
            return true;
        }
        regex = null;
        return false;
    }

    internal bool IsRegexString([MaybeNullWhen(false)] out RegexStringHolder regexString)
    {
        if (Kind == KindAndFlags.RegexString)
        {
            regexString = (RegexStringHolder)_data!;
            return true;
        }
        regexString = null;
        return false;
    }

    internal bool IsAccept([MaybeNullWhen(false)] out Regex regex, out TokenSymbolHandle symbol, out bool lowestPriority)
    {
        symbol = new((uint)M + 1);
        lowestPriority = N != 0;
        if (Kind == KindAndFlags.Accept)
        {
            regex = (Regex)_data!;
            return true;
        }
        regex = null;
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
            switch (_kindAndFlags & KindAndFlags.CaseMask)
            {
                case KindAndFlags.CaseSensitive:
                    isCaseOverridden = true;
                    return true;
                case KindAndFlags.CaseInsensitive:
                    isCaseOverridden = true;
                    return false;
            }
        }

        return existingIsCaseSensitive;
    }

    internal bool TryGetCaseSensitivity(out bool isCaseSensitive)
    {
        switch (_kindAndFlags & KindAndFlags.CaseMask)
        {
            case KindAndFlags.CaseSensitive:
                isCaseSensitive = true;
                return true;
            case KindAndFlags.CaseInsensitive:
                isCaseSensitive = false;
                return true;
            default:
                isCaseSensitive = false;
                return false;
        }
    }

    /// <summary>
    /// A <see cref="Regex"/> that matches any character.
    /// </summary>
    public static Regex Any { get; } = new(KindAndFlags.Any, null);

    /// <summary>
    /// A <see cref="Regex"/> that matches the empty string.
    /// </summary>
    public static Regex Empty { get; } = new(KindAndFlags.Concat, (Regex[])[]);

    /// <summary>
    /// A <see cref="Regex"/> that does not match anything.
    /// </summary>
    internal static Regex Void { get; } = new(KindAndFlags.Alt, (Regex[])[]);

    /// <summary>
    /// Creates a <see cref="Regex"/> that causes the DFA to accept a token symbol
    /// after matching the given <see cref="Regex"/>.
    /// </summary>
    /// <param name="regex"></param>
    /// <param name="symbol">A handle to the token symbol to accept.</param>
    /// <param name="lowestPriority">Whether <paramref name="symbol"/> is given the lowest
    /// priority when resolving conflicts. Conflicts between symbols with the lowest priority
    /// get randomly resolved.</param>
    /// <remarks>
    /// Accept nodes are only used internally by the builder.
    /// </remarks>
    internal static Regex Accept(Regex regex, TokenSymbolHandle symbol, bool lowestPriority)
    {
        Debug.Assert(symbol.HasValue);
        return new(KindAndFlags.Accept, regex, symbol.Value, lowestPriority ? 1 : 0);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches some specific characters.
    /// </summary>
    /// <param name="chars">The characters to match, or to avoid matching.</param>
    /// <param name="flags">Flags to customize the regex's matching behavior.</param>
    /// <seealso cref="OneOf(ImmutableArray{char})"/>
    /// <seealso cref="NotOneOf(ImmutableArray{char})"/>
    internal static Regex Chars(ImmutableBuffer<char> chars, CharsFlags flags)
    {
        Debug.Assert((flags & ~CharsFlags.All) == 0);
        KindAndFlags regexFlags = KindAndFlags.Chars | (KindAndFlags)flags;
        return new(regexFlags, chars.RawValue);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches some specific character ranges.
    /// </summary>
    /// <param name="chars">The character ranges to match, or to avoid matching.</param>
    /// <param name="flags">Flags to customize the regex's matching behavior.</param>
    /// <seealso cref="OneOf(ImmutableArray{ValueTuple{char, char}})"/>
    /// <seealso cref="NotOneOf(ImmutableArray{ValueTuple{char, char}})"/>
    internal static Regex CharRanges(ImmutableArray<(char, char)> chars, CharsFlags flags)
    {
        Debug.Assert(!chars.IsDefault);
        Debug.Assert((flags & ~CharsFlags.All) == 0);
        KindAndFlags regexFlags = KindAndFlags.CharRanges | (KindAndFlags)flags;
        return new(regexFlags, ImmutableCollectionsMarshal.AsArray(chars));
    }

    /// <summary>
    /// A <see cref="Regex"/> that matches a specific character.
    /// </summary>
    public static Regex Literal(char c) => OneOf(c);

    /// <summary>
    /// A <see cref="Regex"/> that matches a specific string of characters.
    /// </summary>
    public static Regex Literal(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (s.Length == 0)
        {
            return Empty;
        }
        return new(KindAndFlags.StringLiteral, s);
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
        ArgumentNullException.ThrowIfNull(pattern);
        return new(KindAndFlags.RegexString, RegexStringHolder.Create(pattern));
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches all characters except
    /// of specific ones.
    /// </summary>
    /// <param name="chars">The characters to not match.</param>
    public static Regex NotOneOf(params ImmutableArray<char> chars)
    {
        char[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(chars);
        ArgumentNullException.ThrowIfNull(arrayUnsafe, nameof(chars));

        if (arrayUnsafe.Length == 0)
        {
            return Any;
        }

        return new(KindAndFlags.Chars | KindAndFlags.Inverted, arrayUnsafe);
    }

    /// <inheritdoc cref="NotOneOf(ImmutableArray{char})"/>
    [ExcludeFromCodeCoverage]
    public static Regex NotOneOf(IEnumerable<char> chars)
    {
        ArgumentNullException.ThrowIfNull(chars);

        var charsBuffer = chars is string s ? ImmutableBuffer.Create(s) : ImmutableBuffer.Create(chars.ToImmutableArray());
        if (charsBuffer.IsEmpty)
        {
            return Any;
        }

        return new(KindAndFlags.Chars | KindAndFlags.Inverted, charsBuffer.RawValue);
    }

    /// <inheritdoc cref="NotOneOf(ImmutableArray{char})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static Regex NotOneOf(params char[] chars) => NotOneOf(chars.ToImmutableArrayChecked());

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches all characters except
    /// of those in specific ranges.
    /// </summary>
    /// <param name="ranges">The character ranges to not match, inclusive.</param>
    /// <exception cref="ArgumentException">A range's start is greater
    /// than its end.</exception>
    public static Regex NotOneOf(params ImmutableArray<(char Start, char End)> ranges)
    {
        (char, char)[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(ranges);
        ArgumentNullException.ThrowIfNull(arrayUnsafe, nameof(ranges));
        ValidateCharacterRange(arrayUnsafe.AsSpan());

        if (arrayUnsafe.Length == 0)
        {
            return Any;
        }

        return new(KindAndFlags.CharRanges | KindAndFlags.Inverted, arrayUnsafe);
    }

    /// <inheritdoc cref="NotOneOf(ImmutableArray{ValueTuple{char, char}})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static Regex NotOneOf(params (char Start, char End)[] chars) => NotOneOf(chars.ToImmutableArrayChecked());

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches specific characters.
    /// </summary>
    /// <param name="chars">The characters to match.</param>
    /// <remarks>
    /// Passing an empty array to <paramref name="chars"/> will result in
    /// a regex that cannot match anything. This is usually not desirable
    /// and will result in a build-time warning.
    /// </remarks>
    public static Regex OneOf(params ImmutableArray<char> chars)
    {
        char[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(chars);
        ArgumentNullException.ThrowIfNull(arrayUnsafe, nameof(chars));

        if (arrayUnsafe.Length == 0)
        {
            return Void;
        }

        return new(KindAndFlags.Chars, arrayUnsafe);
    }

    /// <inheritdoc cref="OneOf(ImmutableArray{char})"/>
    public static Regex OneOf(IEnumerable<char> chars)
    {
        ArgumentNullException.ThrowIfNull(chars);

        var charsBuffer = chars is string s ? ImmutableBuffer.Create(s) : ImmutableBuffer.Create(chars.ToImmutableArray());
        if (charsBuffer.IsEmpty)
        {
            return Void;
        }

        return new(KindAndFlags.Chars, charsBuffer.RawValue);
    }

    /// <inheritdoc cref="OneOf(ImmutableArray{char})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static Regex OneOf(params char[] chars) => OneOf(chars.ToImmutableArrayChecked());

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
    public static Regex OneOf(params ImmutableArray<(char, char)> ranges)
    {
        (char, char)[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(ranges);
        ArgumentNullException.ThrowIfNull(arrayUnsafe, nameof(ranges));
        ValidateCharacterRange(arrayUnsafe.AsSpan());

        if (arrayUnsafe.Length == 0)
        {
            return Void;
        }

        return new(KindAndFlags.CharRanges, arrayUnsafe);
    }

    /// <inheritdoc cref="OneOf(ImmutableArray{ValueTuple{char, char}})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static Regex OneOf(params (char Start, char End)[] chars) => OneOf(chars.ToImmutableArrayChecked());

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches many regexes in sequence.
    /// </summary>
    /// <param name="regexes">The regexes to concatenate.</param>
    public static Regex Join(params ImmutableArray<Regex> regexes)
    {
        Regex[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(regexes);
        ArgumentNullException.ThrowIfNull(arrayUnsafe, nameof(regexes));
        foreach (Regex regex in arrayUnsafe)
            ArgumentNullException.ThrowIfNull(regex, nameof(regexes));

        return arrayUnsafe switch
        {
            [] => Empty,
            [var x] => x,
            _ => new(KindAndFlags.Concat, arrayUnsafe),
        };
    }

    /// <inheritdoc cref="Join(ImmutableArray{Regex})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static Regex Join(params Regex[] regexes) => Join(regexes.ToImmutableArrayChecked());

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches either one of many regexes.
    /// </summary>
    /// <param name="regexes">The regexes to choose form.</param>
    /// <remarks>
    /// Passing an empty array to <paramref name="regexes"/> will result in
    /// a regex that cannot match anything. This is usually not desirable
    /// and will result in a build-time warning.
    /// </remarks>
    public static Regex Choice(params ImmutableArray<Regex> regexes)
    {
        Regex[]? arrayUnsafe = ImmutableCollectionsMarshal.AsArray(regexes);
        ArgumentNullException.ThrowIfNull(arrayUnsafe, nameof(regexes));
        foreach (Regex regex in arrayUnsafe)
            ArgumentNullException.ThrowIfNull(regex, nameof(regexes));

        return arrayUnsafe switch
        {
            [] => Void,
            [var x] => x,
            _ => new(KindAndFlags.Alt, arrayUnsafe),
        };
    }

    /// <inheritdoc cref="Choice(ImmutableArray{Regex})"/>
    [ExcludeFromCodeCoverage, OverloadResolutionPriority(-1)]
    public static Regex Choice(params Regex[] regexes) => Choice(regexes.ToImmutableArrayChecked());

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
    /// <param name="times">The number of times to repeat the regex.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="times"/>
    /// is negative, or equal to <see cref="int.MaxValue"/>.</exception>
    public Regex Repeat(int times)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(times);
        return times switch
        {
            0 => Empty,
            1 => this,
            _ => Between(times, times)
        };
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex either once or
    /// not at all.
    /// </summary>
    public Regex Optional() => this is { Kind: KindAndFlags.Loop, M: 0 } ? this : Loop(0, 1);

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex a number of times
    /// within a range.
    /// </summary>
    /// <param name="minTimes">The minimum number of times to repeat the regex, inclusive.</param>
    /// <param name="maxTimes">The maximum number of times to repeat the regex, inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minTimes"/> or
    /// <paramref name="maxTimes"/> is negative, <paramref name="minTimes"/> is greater than
    /// <paramref name="maxTimes"/>, or <paramref name="maxTimes"/> is equal to
    /// <see cref="int.MaxValue"/>.</exception>
    public Regex Between(int minTimes, int maxTimes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minTimes);
        if (minTimes > maxTimes)
        {
            throw new ArgumentException(Resources.Builder_RegexLoopRangeReverseOrder);
        }
        if (maxTimes == int.MaxValue)
        {
            throw new ArgumentException(Resources.Builder_RegexLoopMaxTooBig);
        }

        return Loop(minTimes, maxTimes);
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that matches this regex at least a specific
    /// number of times.
    /// </summary>
    /// <param name="minTimes">The minimum number of times to repeat the regex, inclusive.</param>
    public Regex AtLeast(int minTimes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minTimes);
        return Loop(minTimes, int.MaxValue);
    }

    private Regex WithCase(KindAndFlags @case)
    {
        Debug.Assert(@case is KindAndFlags.CaseSensitive or KindAndFlags.CaseInsensitive);
        if ((_kindAndFlags & KindAndFlags.CaseMask) == @case)
            return this;
        return new(_kindAndFlags & ~KindAndFlags.CaseMask | @case, _data, M, N);
    }

    /// <summary>
    /// Creates a case-sensitive copy of this <see cref="Regex"/>.
    /// </summary>
    public Regex CaseSensitive() => WithCase(KindAndFlags.CaseSensitive);

    /// <summary>
    /// Creates a case-insensitive copy of this <see cref="Regex"/>.
    /// </summary>
    public Regex CaseInsensitive() => WithCase(KindAndFlags.CaseInsensitive);

    /// <summary>
    /// Concatenates two <see cref="Regex"/> objects.
    /// </summary>
    /// <param name="left">The first regex.</param>
    /// <param name="right">The second regex.</param>
    /// <returns>A <see cref="Regex"/> that matches <paramref name="left"/>
    /// and then <paramref name="right"/> in sequence.</returns>
    /// <seealso cref="Join(ImmutableArray{Regex})"/>
    public static Regex operator +(Regex left, Regex right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

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
        return Join(left, right);
    }

    /// <summary>
    /// Combines two <see cref="Regex"/> objects with an OR operator.
    /// </summary>
    /// <param name="left">The first regex.</param>
    /// <param name="right">The second regex.</param>
    /// <returns>A <see cref="Regex"/> that matches either
    /// <paramref name="left"/> or <paramref name="right"/>.</returns>
    /// <seealso cref="Choice(ImmutableArray{Regex})"/>
    public static Regex operator |(Regex left, Regex right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

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
            // Farkle 6 also optimized patterns with inverted Chars, but that would
            // involve intersecting or taking the difference of the two sets, and
            // it's not likely to occur either way.
            if (left.IsChars(out var leftChars, out var leftFlags) &&
                right.IsChars(out var rightChars, out var rightFlags) &&
                ((leftFlags | rightFlags) & CharsFlags.Inverted) == 0)
            {
                return OneOf([.. leftChars.Span, .. rightChars.Span]);
            }
            if (left.IsCharRanges(out var leftRanges, out leftFlags) &&
                right.IsCharRanges(out var rightRanges, out rightFlags) &&
                ((leftFlags | rightFlags) & CharsFlags.Inverted) == 0)
            {
                return OneOf([.. leftRanges, .. rightRanges]);
            }
        }
        return Choice(left, right);
    }

    /// <summary>
    /// Concatenates this <see cref="Regex"/> with another one.
    /// Obsolete, use <see cref="operator +(Regex, Regex)"/> instead.
    /// </summary>
    /// <param name="other">The other regex.</param>
    [EditorBrowsable(EditorBrowsableState.Never), ExcludeFromCodeCoverage]
    [Obsolete("Use the + operator instead."
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.RegexAndOrCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
    )]
    public Regex And(Regex other) => this + other;

    /// <summary>
    /// Combines this <see cref="Regex"/> with another one using an OR operator.
    /// Obsolete, use <see cref="operator |(Regex, Regex)"/> instead.
    /// </summary>
    /// <param name="other">The other regex.</param>
    [EditorBrowsable(EditorBrowsableState.Never), ExcludeFromCodeCoverage]
    [Obsolete("Use the | operator instead."
#if NET5_0_OR_GREATER
        , DiagnosticId = Obsoletions.RegexAndOrCode, UrlFormat = Obsoletions.SharedUrlFormat
#endif
    )]
    public Regex Or(Regex other) => this | other;

    [Flags]
    private enum KindAndFlags : uint
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
        /// The regex matches certain characters.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be a <see cref="string"/>, or an array of <see cref="char"/>.
        /// </remarks>
        Chars = 2,
        /// <summary>
        /// The regex matches certain character ranges.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be an array of value 2-tuples of <see cref="char"/>.
        /// </remarks>
        CharRanges = 3,
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
        /// The regex represents accepting a token symbol after matching another regex.
        /// This kind is only used internally by the builder.
        /// </summary>
        /// <remarks>
        /// <see cref="_data"/> must be a <see cref="Regex"/>.
        /// <see cref="M"/> contains the token symbol's <see cref="TokenSymbolHandle.TableIndex"/>,
        /// and <see cref="N"/> contains whether the symbol has a lowest accept priority.
        /// </remarks>
        Accept = 8,
        /// <summary>
        /// A mask for the regex kind bits.
        /// </summary>
        KindMask = 0x0F,
        /// <summary>
        /// The regex will stop being propagated to accepting DFA states.
        /// </summary>
        /// <remarks>
        /// Specifically, this means that in the DFA builder, when taking the <c>followPos</c> of a
        /// set of leaves, if one of the input leaves has this flag, and one of the output leaves is
        /// an <c>End</c> leaf, the <c>followPos</c> will be computed again, but excluding the leaves
        /// with this flag from the input. This new set of leaves will be used, even if it does not
        /// contain an <c>End</c> leaf.
        /// </remarks>
        BreakOnAccept = 0x08000000,
        /// <summary>
        /// The regex will match any character except those specified. Valid only in regexes of
        /// kind <see cref="Chars"/>.
        /// </summary>
        Inverted = 0x10000000,
        /// <summary>
        /// The regex will match any character except those specified, but also force failing the
        /// tokenizer if one of those characters is encountered, even if another regex can match it.
        /// Implies <see cref="Inverted"/>. Valid only in regexes of kind <see cref="Chars"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Usually, when there are more than one regexes that can be advanced by a character at a
        /// given DFA state (e.g. <c>aaa|abb</c>), the DFA will match both at the same time, and a
        /// conflict will occur if the same state ends up accepting more than one token symbol.
        /// </para>
        /// <para>
        /// However, in a pattern like <c>aaa|[^a]</c>, the DFA can either advance the first alternative
        /// when encountering <c>a</c>, or fail matching per the second alternative. By default, Farkle
        /// will prefer advancing the first alternative, but if the second alternative had this flag, it
        /// will emit a failing transition for the <c>a</c> character.
        /// </para>
        /// </remarks>
        HighPriorityInverted = 0x30000000,
        /// <summary>
        /// The regex is forced to be case-sensitive.
        /// </summary>
        CaseSensitive = 0x40000000,
        /// <summary>
        /// The regex is forced to be case-insensitive.
        /// </summary>
        CaseInsensitive = 0x80000000,
        /// <summary>
        /// A mask for the case-sensitivity bits.
        /// </summary>
        CaseMask = 0xC0000000,
        /// <summary>
        /// A mask for all regex flag bits.
        /// </summary>
        FlagsMask = 0xF8000000,
    }

    /// <summary>
    /// Contains flags to customize the matching behavior of a regex of kinds
    /// <see cref="KindAndFlags.Chars"/> or <see cref="KindAndFlags.CharRanges"/>.
    /// </summary>
    [Flags]
    internal enum CharsFlags : uint
    {
        None = 0,
        BreakOnAccept = KindAndFlags.BreakOnAccept,
        Inverted = KindAndFlags.Inverted,
        HighPriorityInverted = KindAndFlags.HighPriorityInverted,
        All = BreakOnAccept | Inverted | HighPriorityInverted,
    }
}
