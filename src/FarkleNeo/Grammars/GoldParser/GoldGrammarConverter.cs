// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Buffers;
using Farkle.Grammars.Writers;
using System.Collections.Immutable;
using System.Diagnostics;
using static Farkle.Grammars.GoldParser.GoldGrammar;

namespace Farkle.Grammars.GoldParser;

internal static class GoldGrammarConverter
{
    public static ImmutableArray<byte> Convert(GoldGrammar grammar)
    {
        GrammarWriter writer = new();
        Symbol[] symbols = grammar.Symbols;
        GoldGrammar.Production[] productions = grammar.Productions;
        GoldGrammar.Group[] groups = grammar.Groups;
        ImmutableArray<LalrAction>[] lalrStates = grammar.LalrStates;

        EntityHandle[] symbolMapping = new EntityHandle[symbols.Length];
        ProductionHandle[] productionMapping = new ProductionHandle[productions.Length];

        // We add the token symbols. Terminals must come first.
        AddSymbolsOfType(SymbolKind.Terminal, TokenSymbolAttributes.Terminal);
        AddSymbolsOfType(SymbolKind.Noise, TokenSymbolAttributes.Noise);
        AddSymbolsOfType(SymbolKind.GroupStart, TokenSymbolAttributes.GroupStart);
        AddSymbolsOfType(SymbolKind.GroupEnd, TokenSymbolAttributes.None);

        void AddSymbolsOfType(SymbolKind goldSymbolKind, TokenSymbolAttributes flags)
        {
            for (int i = 0; i < symbols.Length; i++)
            {
                Symbol symbol = symbols[i];
                if (symbol.Kind == goldSymbolKind)
                {
                    StringHandle name = writer.GetOrAddString(symbol.Name);
                    symbolMapping[i] = writer.AddTokenSymbol(name, flags);
                }
            }
        }

        // Then come the groups.
        for (int i = 0; i < groups.Length; i++)
        {
            GoldGrammar.Group group = groups[i];
            StringHandle name = writer.GetOrAddString(group.Name);
            SymbolKind containerKind = symbols[group.ContainerIndex].Kind;
            bool isNewLine = group.Name.Equals("NewLine", StringComparison.OrdinalIgnoreCase);
            if (containerKind != SymbolKind.GroupEnd && !(isNewLine && containerKind is SymbolKind.Terminal or SymbolKind.Noise))
            {
                // Farkle 6 did the same validation.
                ThrowHelpers.ThrowNotSupportedException("Group does not end with a GroupEnd or a newline. Please open an issue on GitHub.");
            }
            TokenSymbolHandle container = (TokenSymbolHandle)symbolMapping[group.ContainerIndex];
            GroupAttributes flags =
                (group.AdvanceByChar ? GroupAttributes.AdvanceByCharacter : 0)
                | (group.KeepEndToken ? GroupAttributes.KeepEndToken : 0)
                | (isNewLine ? GroupAttributes.EndsOnEndOfInput : 0);
            TokenSymbolHandle start = (TokenSymbolHandle)symbolMapping[group.StartIndex];
            TokenSymbolHandle end = (TokenSymbolHandle)symbolMapping[group.EndIndex];
            int nestingCount = group.Nesting.Length;

            uint groupIndex = writer.AddGroup(name, container, flags, start, end, nestingCount);
            Debug.Assert(groupIndex == (uint)i);
        }
        // After all groups are added we add their nestings.
        foreach (GoldGrammar.Group group in groups)
        {
            foreach (ushort nestedGroup in group.Nesting)
            {
                writer.AddGroupNesting(nestedGroup);
            }
        }

        // Adding the nonterminals is a bit complicated because we must know upfront how many productions they have.
        // First we count the nonterminals and define an order among them starting from zero.
        int nonterminalCount = 0;
        int[] nonterminalMapping = new int[symbols.Length];
        // This will catch productions with invalid heads later on.
        nonterminalMapping.AsSpan().Fill(-1);
        for (int i = 0; i < symbols.Length; i++)
        {
            if (symbols[i].Kind == SymbolKind.Nonterminal)
            {
                nonterminalMapping[i] = nonterminalCount++;
            }
        }
        // Now that we know how many terminals we have, we count how many productions each has.
        int[] productionCounts = new int[nonterminalCount];
        foreach (GoldGrammar.Production production in productions)
        {
            productionCounts[nonterminalMapping[production.HeadIndex]]++;
        }
        // And now we know everything we need to add the nonterminals.
        for (int i = 0; i < symbols.Length; i++)
        {
            if (symbols[i] is { Kind: SymbolKind.Nonterminal, Name: string name })
            {
                StringHandle nameHandle = writer.GetOrAddString(name);
                int productionCount = productionCounts[nonterminalMapping[i]];
                symbolMapping[i] = writer.AddNonterminal(nameHandle, 0, productionCount);
            }
        }

        // The productions have the challenge that they need to be added in order their
        // heads were added. Looping over each nonterminal and adding its productions is
        // O(n²) so our only solution is to sort the productions by head. But first we have
        // to create a mapping between the productions' original and new positions.
        // Using a dictionary is easier; with an int[] we would need a custom sorting algorithm.
        Dictionary<GoldGrammar.Production, int> productionOriginalPositions = new();
        for (int i = 0; i < productions.Length; i++)
        {
            productionOriginalPositions[productions[i]] = i;
        }
        // We could have sorted the original array but let's not; it's supposed to be immutable.
        GoldGrammar.Production[] sortedProductions = productions.AsSpan().ToArray();
        // Because the nonterminals were added in increasing order of appearance,
        // sorting by their original head index is the same as sorting by their mapped index.
        // The algorithm does not need to be stable.
        Array.Sort(sortedProductions, (x1, x2) => x1.HeadIndex.CompareTo(x2.HeadIndex));
        foreach (GoldGrammar.Production production in sortedProductions)
        {
            productionMapping[productionOriginalPositions[production]] =
                writer.AddProduction(production.Members.Length);

            foreach (ushort member in production.Members)
            {
                writer.AddProductionMember(symbolMapping[member]);
            }
        }

        // The state machines are straightforward.
        LrWriter lr = new(grammar.LalrStates.Length);
        foreach (ImmutableArray<LalrAction> actions in grammar.LalrStates)
        {
            foreach (ref readonly LalrAction action in actions.AsSpan())
            {
                switch (action.Kind, symbols[action.SymbolIndex].Kind)
                {
                    case (LalrActionKind.Shift, SymbolKind.Terminal):
                        lr.AddShift((TokenSymbolHandle)symbolMapping[action.SymbolIndex], action.TargetIndex);
                        break;
                    case (LalrActionKind.Reduce, SymbolKind.Terminal):
                        ProductionHandle production = productionMapping[action.TargetIndex];
                        lr.AddReduce((TokenSymbolHandle)symbolMapping[action.SymbolIndex], production);
                        break;
                    case (LalrActionKind.Reduce, SymbolKind.EndOfFile):
                        lr.AddEofReduce(productionMapping[action.TargetIndex]);
                        break;
                    case (LalrActionKind.Goto, SymbolKind.Nonterminal):
                        lr.AddGoto((NonterminalHandle)symbolMapping[action.SymbolIndex], action.TargetIndex);
                        break;
                    case (LalrActionKind.Accept, SymbolKind.EndOfFile):
                        lr.AddEofAccept();
                        break;
                    default:
                        ThrowHelpers.ThrowInvalidDataException("Invalid LALR state.");
                        break;
                }
            }
            lr.FinishState();
        }
        if (lr.HasConflicts)
        {
            ThrowHelpers.ThrowInvalidDataException("LALR states have conflicts.");
        }
        writer.AddStateMachine(lr);

        DfaWriter<char> dfa = new(grammar.DfaStates.Length);
        foreach (DfaState state in grammar.DfaStates)
        {
            foreach ((uint charSet, ushort target) in state.Edges)
            {
                foreach ((char cFrom, char cTo) in grammar.CharacterSets[charSet])
                {
                    dfa.AddEdge(cFrom, cTo, target);
                }
            }
            if (state.AcceptIndex is ushort acceptIndex)
            {
                dfa.AddAccept((TokenSymbolHandle)symbolMapping[acceptIndex]);
            }
            dfa.FinishState();
        }
        // We will call AddAccept at most once for each state. We can't have conflicts.
        Debug.Assert(!dfa.HasConflicts);
        writer.AddStateMachine(dfa);

        // We leave the grammar information for the end.
        StringHandle grammarNameHandle = writer.GetOrAddString(grammar.Name);
        NonterminalHandle startSymbol = (NonterminalHandle)symbolMapping[grammar.StartSymbolIndex];
        writer.SetGrammarInfo(grammarNameHandle, startSymbol, 0);

        // And finally write the converted grammar.
        using PooledSegmentBufferWriter<byte> bufferWriter = new();
        writer.WriteTo(bufferWriter);
        return bufferWriter.ToImmutableArray();
    }
}
