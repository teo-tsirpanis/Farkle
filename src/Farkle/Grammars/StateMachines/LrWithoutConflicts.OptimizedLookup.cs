// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Farkle.Grammars.StateMachines;

internal partial class LrWithoutConflicts
{
    private OptimizedLookup? _optimizedLookup;

    public OptimizedLookup GetOptimizedLookup() => _optimizedLookup ??= new(this);

    internal sealed class OptimizedLookup
    {
        private readonly int[] _actionOffsets;
        private readonly LrAction[] _actions;
        private readonly TokenSymbolHandle[] _checkTerminals;

        private readonly LrAction[] _defaultActions;

        private readonly int[] _gotoOffsets;
        private readonly int[] _gotos;

        private readonly LrEndOfFileAction[] _eofActions;

        public OptimizedLookup(LrWithoutConflicts lr)
        {
            var grammarFile = lr.Grammar.GrammarFile;
            var stateIndices = new int[lr.Count];
            var assignedOffsets = new HashSet<int>(lr.Count);
            for (int i = 0; i < stateIndices.Length; i++)
            {
                stateIndices[i] = i;
            }
            var stateActionGaps = new int[lr.Count];

            {
                for (int i = 0; i < stateActionGaps.Length; i++)
                {
                    stateActionGaps[i] = lr.GetActionGapCountUnsafe(grammarFile, i);
                }
                Array.Sort(stateIndices, (a, b) => -stateActionGaps[a].CompareTo(stateActionGaps[b]));

                _actionOffsets = new int[lr.Count];
                var stateDict = new Dictionary<int, int>(lr.Count, new StateActionComparer(lr));
                var actions = new List<LrAction>();
                var terminals = new List<TokenSymbolHandle>();
                _defaultActions = new LrAction[lr.Count];
                _eofActions = new LrEndOfFileAction[lr.Count];
                foreach (int state in stateIndices)
                {
                    if (lr.DefaultReductionBase != 0)
                    {
                        _defaultActions[state] = lr.ReadDefaultReduction(grammarFile, state) switch
                        {
                            { HasValue: true } prod => LrAction.CreateReduce(prod),
                            _ => LrAction.Error,
                        };
                    }
                    _eofActions[state] = lr.ReadEofAction(grammarFile, state);

                    var (actOffset, actCount) = lr.GetActionBoundsUnsafe(grammarFile, state);
                    if (actCount == 0)
                    {
                        _actionOffsets[state] = int.MinValue;
                        continue;
                    }
                    if (stateDict.TryGetValue(state, out int existingOffset))
                    {
                        _actionOffsets[state] = existingOffset;
                        continue;
                    }
                    int offset = 0;
                    int firstTerminal = lr.ReadActionTerminal(grammarFile, actOffset).Value;
                    bool offsetIsValid = false;

                    while (!offsetIsValid)
                    {
                        offsetIsValid = true;
                        for (int j = actOffset; j < actOffset + actCount; j++)
                        {
                            var terminal = lr.ReadActionTerminal(grammarFile, j).Value;
                            var idx = offset + terminal - firstTerminal;
                            if (idx >= terminals.Count)
                            {
                                break;
                            }
                            if (terminals[idx].HasValue)
                            {
                                offsetIsValid = false;
                                offset++;
                                break;
                            }
                        }
                        if (offsetIsValid && !assignedOffsets.Add(offset - firstTerminal))
                        {
                            offsetIsValid = false;
                            offset++;
                        }
                    }

                    offset -= firstTerminal;
                    _actionOffsets[state] = offset;
                    stateDict[state] = offset;

                    int maxIndex = offset + lr.ReadActionTerminal(grammarFile, actOffset + actCount - 1).Value;
                    EnsureIndex(actions, maxIndex);
                    EnsureIndex(terminals, maxIndex);
                    for (int j = actOffset; j < actOffset + actCount; j++)
                    {
                        var (token, action) = lr.GetActionAtUnsafe(grammarFile, j);
                        var idx = offset + token.Value;
                        actions[idx] = action;
                        terminals[idx] = token;
                    }
                }
                _actions = actions.ToArray();
                _checkTerminals = terminals.ToArray();
            }

            {
                for (int i = 0; i < lr.Count; i++)
                {
                    stateActionGaps[i] = lr.GetGotoGapCountUnsafe(grammarFile, i);
                }
                Array.Sort(stateIndices, (a, b) => -stateActionGaps[a].CompareTo(stateActionGaps[b]));

                _gotoOffsets = new int[lr.Count];
                var stateDict = new Dictionary<int, int>(lr.Count, new StateGotoComparer(lr));
                assignedOffsets.Clear();
                var gotos = new List<int>();
                var occupiedSlots = new List<bool>();
                foreach (int state in stateIndices)
                {
                    var (gotoOffset, gotoCount) = lr.GetGotoBoundsUnsafe(grammarFile, state);
                    if (gotoCount == 0)
                    {
                        _gotoOffsets[state] = int.MinValue;
                        continue;
                    }
                    if (stateDict.TryGetValue(state, out int existingOffset))
                    {
                        _gotoOffsets[state] = existingOffset;
                        continue;
                    }
                    int offset = 0;
                    int firstNonterminal = lr.ReadGotoNonterminal(grammarFile, gotoOffset).Value;
                    bool offsetIsValid = false;

                    while (!offsetIsValid)
                    {
                        offsetIsValid = true;
                        for (int j = gotoOffset; j < gotoOffset + gotoCount; j++)
                        {
                            var nonterminal = lr.ReadGotoNonterminal(grammarFile, j).Value;
                            var idx = offset + nonterminal - firstNonterminal;
                            if (idx >= occupiedSlots.Count)
                            {
                                break;
                            }
                            if (occupiedSlots[idx])
                            {
                                offsetIsValid = false;
                                offset++;
                                break;
                            }
                        }
                        if (offsetIsValid && assignedOffsets.Add(offset))
                        {
                            stateDict[state] = offset;
                        }
                    }

                    offset -= firstNonterminal;
                    _gotoOffsets[state] = offset;
                    stateDict[state] = offset;

                    int maxIndex = offset + lr.ReadGotoNonterminal(grammarFile, gotoOffset + gotoCount - 1).Value;
                    EnsureIndex(gotos, maxIndex, fillValue: -1);
                    EnsureIndex(occupiedSlots, maxIndex);
                    for (int j = gotoOffset; j < gotoOffset + gotoCount; j++)
                    {
                        var (nonterminal, targetState) = lr.GetGotoAtUnsafe(grammarFile, j);
                        var idx = offset + nonterminal.Value;
                        gotos[idx] = targetState;
                        occupiedSlots[idx] = true;
                    }
                }
                _gotos = gotos.ToArray();
            }

#if DEBUG
            for (int i = 0; i < lr.Count; i++)
            {
                var (actOffset, actCount) = lr.GetActionBoundsUnsafe(grammarFile, i);
                for (int j = actOffset; j < actOffset + actCount; j++)
                {
                    var (token, action) = lr.GetActionAtUnsafe(grammarFile, j);
                    Debug.Assert(GetAction(i, token) == action);
                }
                var (gotoOffset, gotoCount) = lr.GetGotoBoundsUnsafe(grammarFile, i);
                for (int j = gotoOffset; j < gotoOffset + gotoCount; j++)
                {
                    var (nonterminal, targetState) = lr.GetGotoAtUnsafe(grammarFile, j);
                    Debug.Assert(GetGoto(i, nonterminal) == targetState);
                }
            }
#endif

            static void EnsureIndex<T>(List<T> list, int maxIndex, T? fillValue = default)
            {
                if (list.Count is var oldCount && maxIndex >= oldCount)
                {
                    CollectionsMarshal.SetCount(list, maxIndex + 1);
                    CollectionsMarshal.AsSpan(list)[oldCount..].Fill(fillValue!);
                }
            }
        }

        public LrAction GetAction(int state, TokenSymbolHandle terminal)
        {
            Debug.Assert(terminal.HasValue);
            int offset = _actionOffsets[state];
            int index = offset + (int)terminal.TableIndex - 1;
            if ((uint)index < (uint)_actions.Length && _checkTerminals[index] == terminal)
            {
                return _actions[index];
            }
            return _defaultActions[state];
        }

        public LrAction GetDefaultAction(int state) => _defaultActions[state];

        public int GetGoto(int state, NonterminalHandle nonterminal)
        {
            Debug.Assert(nonterminal.HasValue);
            int offset = _gotoOffsets[state];
            int index = offset + (int)nonterminal.TableIndex - 1;
            // We don't need a check table for gotos because assuming the state machine is well-formed,
            // each state will have a fixed set of symbols it can use to go to.
            return _gotos[index];
        }

        public LrEndOfFileAction GetEndOfFileAction(int state) => _eofActions[state];
    }

    private sealed class StateActionComparer(LrWithoutConflicts lr) : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => lr.AreActionsEqualUnsafe(lr.Grammar.GrammarFile, x, y);

        public int GetHashCode(int obj) => lr.GetActionHashCodeUnsafe(lr.Grammar.GrammarFile, obj);
    }

    private sealed class StateGotoComparer(LrWithoutConflicts lr) : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => lr.AreGotosEqualUnsafe(lr.Grammar.GrammarFile, x, y);

        public int GetHashCode(int obj) => lr.GetGotoHashCodeUnsafe(lr.Grammar.GrammarFile, obj);
    }
}
