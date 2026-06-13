// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Numerics;
using Farkle.Grammars.StateMachines;

namespace Farkle.Diagnostics.Builder;

/// <summary>
/// Contains logic to generate sequences of characters that lead to specific states of a <see cref="Dfa{TChar}"/>.
/// </summary>
/// <typeparam name="TChar">The type of characters the DFA accepts.
/// Typically it is <see cref="char"/> or <see cref="byte"/>.</typeparam>
/// <remarks>
/// For optimal performance, instances of this class should be reused when operating on the same DFA.
/// </remarks>
internal sealed class DfaWordGenerator<TChar>(Dfa<TChar> dfa) where TChar : unmanaged, INumberBase<TChar>, IMinMaxValue<TChar>
{
    private readonly (int Predecessor, TChar Character)[] _statePredecessors = BuildStatePredecessors(dfa);

    private readonly Dfa<TChar> _dfa = dfa;

    /// <summary>
    /// Enumerates the first and last characters of gaps in a DFA state.
    /// A gap is an interval of unassigned characters.
    /// </summary>
    private static IEnumerable<TChar> EnumerateGaps(DfaState<TChar> state)
    {
        TChar gapStart = TChar.MinValue;
        bool hasLastCharacter = false;
        foreach (var edge in state.Edges)
        {
            if (edge.KeyFrom != gapStart)
            {
                // We could yield tuples but we don't need to, and
                // a flat character sequence makes things simpler.
                yield return gapStart;
                yield return edge.KeyFrom - TChar.One;
            }
            gapStart = edge.KeyTo + TChar.One;
            hasLastCharacter = edge.KeyTo == TChar.MaxValue;
        }
        if (!hasLastCharacter)
        {
            yield return gapStart;
            yield return TChar.MaxValue;
        }
    }

    private static TChar GetUnassignedCharacter(DfaState<TChar> state)
    {
        // Try character 'a' first.
        var a = TChar.CreateChecked('a');
        if (!state.HasEdge(a))
        {
            return a;
        }
        bool seenFirst = false;
        TChar firstGap = default;
        foreach (var gap in EnumerateGaps(state))
        {
            if (!seenFirst)
            {
                firstGap = gap;
                seenFirst = true;
            }
            char c = (char)ushort.CreateChecked(gap);
            // Try finding a user-friendly character at the edges of a gap.
            if (!char.IsControl(c) && !char.IsWhiteSpace(c) && !char.IsSeparator(c) && !char.IsSurrogate(c))
            {
                return gap;
            }
        }
        // We can't fail this one; the builder removes default transitions on complete states.
        Debug.Assert(seenFirst);
        return firstGap;
    }

    private static (int Predecessor, TChar Character)[] BuildStatePredecessors(Dfa<TChar> dfa)
    {
        var statePredecessors = new (int Predecessor, TChar Character)[dfa.Count];
        Array.Fill(statePredecessors, (-1, default));
        var q = new Queue<int>();
        q.Enqueue(0);
        while (q.TryDequeue(out int i))
        {
            DfaState<TChar> state = dfa[i];
            foreach (var edge in state.Edges)
            {
                var targetStateIndex = edge.Target;
                if (targetStateIndex == -1)
                {
                    continue;
                }
                if (statePredecessors[targetStateIndex].Predecessor == -1)
                {
                    statePredecessors[targetStateIndex] = (state.StateIndex, edge.KeyFrom);
                    q.Enqueue(targetStateIndex);
                }
            }
            if (state.DefaultTransition is int dt && dt != -1 && statePredecessors[dt].Predecessor == -1)
            {
                statePredecessors[dt] = (state.StateIndex, GetUnassignedCharacter(state));
                q.Enqueue(dt);
            }
        }
        return statePredecessors;
    }

    public int GetDistanceToAcceptingState(int stateIndex)
    {
        int distance = 0;
        while (stateIndex != _dfa.StartState)
        {
            int pred = _statePredecessors[stateIndex].Predecessor;
            if (pred == -1)
            {
                return -1; // Unreachable state
            }
            distance++;
            stateIndex = pred;
        }
        return distance;
    }

    public int GenerateWord(int stateIndex, Span<TChar> buffer)
    {
        int i;
        for (i = 0; i < buffer.Length && stateIndex > 0; i++)
        {
            (stateIndex, buffer[i]) = _statePredecessors[stateIndex];
        }
        buffer[..i].Reverse();
        return i;
    }
}

internal static class DfaWordGenerator
{
    extension(DfaWordGenerator<char> generator)
    {
        public string? GenerateWordAsString(int stateIndex)
        {
            int length = generator.GetDistanceToAcceptingState(stateIndex);
            if (length == -1)
            {
                return null;
            }
            return string.Create(length, (generator, stateIndex), static (span, gen) =>
                gen.generator.GenerateWord(gen.stateIndex, span)
            );
        }
    }
}
