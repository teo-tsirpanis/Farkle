// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

internal readonly struct GrammarStateMachines
{
    public readonly GrammarFileSection Dfa, DfaDefaultTransitions, DfaWithConflicts;

    public readonly GrammarFileSection Lr1, Glr1;

    public GrammarStateMachines(ReadOnlySpan<byte> grammarFile, in BlobHeap blobHeap, in GrammarTables tables, out bool hasUnknownStateMachines) : this()
    {
        hasUnknownStateMachines = false;
        for (uint i = 0; i < (uint)tables.StateMachineRowCount; i++)
        {
            switch (tables.GetStateMachineKind(grammarFile, i))
            {
                case GrammarConstants.DfaOnCharKind:
                    Dfa = blobHeap.GetBlobSection(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.DfaOnCharWithConflictsKind:
                    DfaWithConflicts = blobHeap.GetBlobSection(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.DfaOnCharDefaultTransitionsKind:
                    DfaDefaultTransitions = blobHeap.GetBlobSection(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.Lr1Kind:
                    Lr1 = blobHeap.GetBlobSection(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.Glr1Kind:
                    Glr1 = blobHeap.GetBlobSection(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                default:
                    hasUnknownStateMachines = true;
                    break;
            }
        }
    }
}
