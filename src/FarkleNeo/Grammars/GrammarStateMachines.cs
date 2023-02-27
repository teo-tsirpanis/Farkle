// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

internal readonly struct GrammarStateMachines
{
    public readonly int DfaOffset, DfaLength;
    public readonly int DfaDefaultTransitionsOffset, DfaDefaultTransitionsLength;
    public readonly int DfaWithConflictsOffset, DfaWithConflictsLength;

    public readonly int Lr1Offset, Lr1Length;
    public readonly int Glr1Offset, Glr1Length;

    public GrammarStateMachines(ReadOnlySpan<byte> grammarFile, in BlobHeap blobHeap, in GrammarTables tables, out bool hasUnknownStateMachines) : this()
    {
        hasUnknownStateMachines = false;
        for (uint i = 0; i < (uint)tables.StateMachineRowCount; i++)
        {
            switch (tables.GetStateMachineKind(grammarFile, i))
            {
                case GrammarConstants.DfaOnCharKind:
                    (DfaOffset, DfaLength) = blobHeap.GetBlobAbsoluteBounds(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.DfaOnCharWithConflictsKind:
                    (DfaWithConflictsOffset, DfaWithConflictsLength) = blobHeap.GetBlobAbsoluteBounds(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.DfaOnCharDefaultTransitionsKind:
                    (DfaDefaultTransitionsOffset, DfaDefaultTransitionsLength) = blobHeap.GetBlobAbsoluteBounds(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.Lr1Kind:
                    (Lr1Offset, Lr1Length) = blobHeap.GetBlobAbsoluteBounds(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                case GrammarConstants.Glr1Kind:
                    (Glr1Offset, Glr1Length) = blobHeap.GetBlobAbsoluteBounds(grammarFile, tables.GetStateMachineData(grammarFile, i));
                    break;
                default:
                    hasUnknownStateMachines = true;
                    break;
            }
        }
    }
}
