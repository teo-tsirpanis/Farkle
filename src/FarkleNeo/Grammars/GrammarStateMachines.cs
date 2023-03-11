// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

internal readonly struct GrammarStateMachines
{
    public readonly GrammarFileSection DfaOnChar, DfaOnCharWithConflicts, DfaOnCharDefaultTransitions;

    public readonly GrammarFileSection Lr1, Glr1;

    public GrammarStateMachines(ReadOnlySpan<byte> grammarFile, in BlobHeap blobHeap, in GrammarTables tables, out bool hasUnknownStateMachines) : this()
    {
        bool seenDfaOnChar = false, seenDfaOnCharWithConflicts = false, seenDfaOnCharDefaultTransitions = false, seenLr1 = false, seenGlr1 = false;

        hasUnknownStateMachines = false;
        for (uint i = 1; i <= (uint)tables.StateMachineRowCount; i++)
        {
            ulong kind = tables.GetStateMachineKind(grammarFile, i);
            BlobHandle data = tables.GetStateMachineData(grammarFile, i);
            switch (kind)
            {
                case GrammarConstants.DfaOnCharKind:
                    AssignStateMachine(grammarFile, in blobHeap, kind, data, ref DfaOnChar, ref seenDfaOnChar);
                    break;
                case GrammarConstants.DfaOnCharWithConflictsKind:
                    AssignStateMachine(grammarFile, in blobHeap, kind, data, ref DfaOnCharWithConflicts, ref seenDfaOnCharWithConflicts);
                    break;
                case GrammarConstants.DfaOnCharDefaultTransitionsKind:
                    AssignStateMachine(grammarFile, in blobHeap, kind, data, ref DfaOnCharDefaultTransitions, ref seenDfaOnCharDefaultTransitions);
                    break;
                case GrammarConstants.Lr1Kind:
                    AssignStateMachine(grammarFile, in blobHeap, kind, data, ref Lr1, ref seenLr1);
                    break;
                case GrammarConstants.Glr1Kind:
                    AssignStateMachine(grammarFile, in blobHeap, kind, data, ref Glr1, ref seenGlr1);
                    break;
                default:
                    // As with streams, we don't detect duplicate state machines.
                    hasUnknownStateMachines = true;
                    break;
            }
        }
    }

    private static void AssignStateMachine(ReadOnlySpan<byte> grammarFile, in BlobHeap blobHeap, ulong kind, BlobHandle data, ref GrammarFileSection sectionAddress, ref bool seen)
    {
        if (seen)
        {
            ThrowDuplicateStream(kind);
        }
        seen = true;
        sectionAddress = blobHeap.GetBlobSection(grammarFile, data);

        static void ThrowDuplicateStream(ulong kind) =>
            ThrowHelpers.ThrowInvalidDataException($"Duplicate state machine {kind:X8}.");
    }
}
