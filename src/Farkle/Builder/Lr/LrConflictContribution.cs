// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents an action to be taken by an LR state machine, that
/// contributes to a conflict in a state (shift or reduce). The
/// symbol that triggers the action is not included.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
// TODO-CSHARP15: Make this a union of int and Production.
internal readonly struct LrConflictContribution : IComparable<LrConflictContribution>
{
    // An integer encoding the action to be taken.
    // It is:
    // * A positive integer for Shift actions, which is the destination state + 1.
    // * A negative integer for Reduce actions, which is the production index negated.
    // * Zero for Accept actions, which is equivalent to a Reduce action of the start production.
    public int Value { get; }

#if DEBUG
    private readonly AugmentedSyntaxProvider _debugOnlySyntax;
#endif

    private LrConflictContribution(int value, AugmentedSyntaxProvider syntax)
    {
        Value = value;
#if DEBUG
        _debugOnlySyntax = syntax;
#endif
    }

    private string GetDebuggerDisplay()
    {
        if (IsShift(out int state))
        {
            return $"Shift {state}";
        }
        _ = IsReduce(out Production production);
        return $"Reduce {production.GetDebuggerDisplay()}";
    }

    public static LrConflictContribution CreateShift(int state, AugmentedSyntaxProvider syntax) =>
        new(state + 1, syntax);

    public static LrConflictContribution CreateReduce(Production production, AugmentedSyntaxProvider syntax) =>
        new(-production.Index, syntax);

    public bool IsAccept => Value == 0;

    public bool IsShift(out int state)
    {
        if (Value > 0)
        {
            state = Value - 1;
            return true;
        }
        state = 0;
        return false;
    }

    public bool IsReduce(out Production production)
    {
        if (Value <= 0)
        {
#if DEBUG
            production = new(-Value, _debugOnlySyntax);
#else
            production = new(-Value, default);
#endif
            return true;
        }
        production = default;
        return false;
    }

    public int CompareTo(LrConflictContribution other) =>
        // This is used in IELR state compatibility test, to sort the lists of the state and candidate contributions.
        // The order doesn't really matter, as long as it is total.
        Value.CompareTo(other.Value);
}
