// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Grammars.StateMachines;

namespace Farkle.Builder.Lr;

/// <summary>
/// Represents an action to be taken by an LR state machine, along with
/// the symbol that triggers it.
/// </summary>
internal readonly struct LrStateEntry
{
    // The symbol that triggers the action.
    // Must be a TokenSymbolHandle, NonterminalHandle, or default for actions on EOF.
    private readonly EntityHandle _symbol;

    // The action to be taken.
    // It is:
    // * an encoded LrAction for actions on terminals
    // * the destination state for Goto actions
    // * an encoded LrEndOfFileAction for actions on EOF
    private readonly uint _action;

    private LrStateEntry(EntityHandle symbol, uint action)
    {
        _symbol = symbol;
        _action = action;
    }

    public static LrStateEntry Create(TokenSymbolHandle terminal, LrAction action) =>
        new(terminal, (uint)action.Value);

    public static LrStateEntry CreateGoto(NonterminalHandle nonterminal, int destination) =>
        new(nonterminal, (uint)destination);

    public static LrStateEntry CreateEndOfFileAction(LrEndOfFileAction action) =>
        new(default, action.Value);

    public bool IsTerminalAction(out TokenSymbolHandle symbol, out LrAction action)
    {
        if (!_symbol.IsTokenSymbol)
        {
            symbol = default;
            action = default;
            return false;
        }
        symbol = (TokenSymbolHandle)_symbol;
        action = new((int)_action);
        return true;
    }

    public bool IsGoto(out NonterminalHandle nonterminal, out int destination)
    {
        if (!_symbol.IsNonterminal)
        {
            nonterminal = default;
            destination = default;
            return false;
        }
        nonterminal = (NonterminalHandle)_symbol;
        destination = (int)_action;
        return true;
    }

    public bool IsEndOfFileAction(out LrEndOfFileAction action)
    {
        action = new(_action);
        return !_symbol.HasValue;
    }
}
