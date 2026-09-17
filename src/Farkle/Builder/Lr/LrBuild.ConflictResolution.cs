// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using Farkle.Builder.OperatorPrecedence;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Builder.Lr;

internal partial struct LrBuild
{
    private bool HasPrecedenceInfo(Symbol symbol, LrConflictContribution contribution) => GetPrecedence(symbol, contribution) >= 0;

    private int GetPrecedence(Symbol symbol, LrConflictContribution contribution)
    {
        Debug.Assert(symbol.IsTerminal);
        if (OperatorInfoProvider is null)
        {
            return -1;
        }
        if (contribution.IsAccept)
        {
            return -1;
        }
        return OperatorInfoProvider.GetPrecedence(contribution.IsReduce(out Production production) ? TranslateProduction(production) : TranslateTerminal(symbol));
    }

    private readonly struct ConflictDescription
    {
        public int StateIndex { get; }

        public Symbol Symbol { get; }

        public ImmutableArray<LrConflictContribution> Contributions { get; }

        public ConflictDescription(int stateIndex, Symbol symbol, ImmutableArray<LrConflictContribution> contributions)
        {
            Debug.Assert(contributions.Length >= 2);
            Debug.Assert(contributions.Skip(1).All(x => x.IsReduce(out _)));
            StateIndex = stateIndex;
            Symbol = symbol;
            Contributions = contributions;
        }
    }
}
