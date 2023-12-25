// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;

namespace Farkle.Builder.StateMachines;

internal static class RegexRangeCanonicalizer
{
    /// <summary>
    /// Checks if the content of <paramref name="ranges"/> is sorted, non-adjacent and non-overlapping.
    /// </summary>
    public static bool IsCanonical<TChar>(ReadOnlySpan<(TChar, TChar)> ranges) where TChar : unmanaged, IComparable<TChar>
    {
        TChar previousEnd;
        switch (ranges)
        {
            case []: return true;
            case [(_, var x), ..]:
                previousEnd = x;
                break;
        }

        foreach (var (start, end) in ranges[1..])
        {
            // This is checked at the time of the regex's construction.
            Debug.Assert(start.CompareTo(end) <= 0);
            if (start.CompareTo(previousEnd) <= 0)
            {
                return false;
            }
            previousEnd = end;
        }
        return true;
    }

    /// <summary>
    /// Makes the given character ranges sorted, non-adjacent and non-overlapping,
    /// while optionally making them case-insensitive.
    /// </summary>
    public static ImmutableArray<(char, char)> Canonicalize(ReadOnlySpan<(char, char)> ranges, bool caseSensitive)
    {
        if (ranges.IsEmpty)
        {
            return [];
        }

        List<(char, IntervalType)> intervals = [];
        foreach (var (start, end) in ranges)
        {
            if (start > end)
            {
                throw new ArgumentException(Resources.Builder_RegexCharacterRangeReverseOrder, nameof(ranges));
            }
            if (caseSensitive)
            {
                intervals.Add((start, IntervalType.Start));
                intervals.Add((end, IntervalType.End));
            }
            else
            {
                for (char c = start; c <= end; c++)
                {
                    intervals.Add((c, IntervalType.Single));
                    char cLower = char.ToLowerInvariant(c);
                    char cUpper = char.ToUpperInvariant(c);
                    if (cLower != c)
                    {
                        intervals.Add((cLower, IntervalType.Single));
                    }
                    if (cUpper != c)
                    {
                        intervals.Add((cUpper, IntervalType.Single));
                    }
                }
            }
        }
        intervals.Sort();

        return MergeIntervals(intervals);
    }

    private static ImmutableArray<(char, char)> MergeIntervals(List<(char, IntervalType)> intervals)
    {
        // intervals is assumed to be sorted and non-empty.
        var rangesBuilder = ImmutableArray.CreateBuilder<(char, char)>();
        int depth = 0;
        char intervalStart = intervals[0].Item1;
        char? intervalEnd = null;
        foreach (var (c, type) in intervals)
        {
            switch (type)
            {
                case IntervalType.Start:
                    // If we are not inside an interval, and the last interval we have
                    // seen (if any) is not adjacent to this one, start a new interval.
                    if (depth == 0 && (intervalEnd is null || c > intervalEnd.GetValueOrDefault() + 1))
                    {
                        // If we have ended an interval before, add it to the list.
                        if (intervalEnd is char cEnd)
                        {
                            rangesBuilder.Add((intervalStart, cEnd));
                        }
                        intervalStart = c;
                    }
                    depth++;
                    break;
                case IntervalType.End:
                    Debug.Assert(depth > 0);
                    depth--;
                    // If the depth is zero, don't add the interval to the
                    // list yet; the next one might be adjacent.
                    if (depth == 0)
                    {
                        intervalEnd = c;
                    }
                    break;
                case IntervalType.Single:
                    // Single is like a Start and an End at the same time.
                    if (depth == 0)
                    {
                        if (intervalEnd is null || c > intervalEnd.GetValueOrDefault() + 1)
                        {
                            if (intervalEnd is char cEnd)
                            {
                                rangesBuilder.Add((intervalStart, cEnd));
                            }
                            intervalStart = c;
                        }
                        intervalEnd = c;
                    }
                    break;
            }
        }
        Debug.Assert(depth == 0);
        Debug.Assert(intervalEnd is not null);
        rangesBuilder.Add((intervalStart, intervalEnd.GetValueOrDefault()));
        return rangesBuilder.ToImmutable();
    }

    private enum IntervalType
    {
        Start,
        Single,
        End
    }
}
