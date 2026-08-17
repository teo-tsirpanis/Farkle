// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents an action to be taken by an LR state machine, that
/// contributes to a conflict in a state. The symbol that triggers
/// the action is not included.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal readonly struct LrConflictContribution
{
    // The action to be taken.
    // It is:
    // *
    // * an encoded LrAction for actions on terminals
    // * the destination state for Goto actions
    // * an encoded LrEndOfFileAction for actions on EOF
    private readonly int _action;
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

    private string DebuggerDisplay
    {
        get
        {
            if (IsShift(out int state))
            {
                return $"Shift {state}";
            }
            _ = IsReduce(out Production production);
            return $"Reduce {production.DebuggerDisplay}";
        }
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
            production = new(-_action);
#endif
            return true;
        }
        production = default;
        return false;
    }
}
