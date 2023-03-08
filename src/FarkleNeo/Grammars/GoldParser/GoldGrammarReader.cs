// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static Farkle.Grammars.GoldParser.GoldGrammar;

namespace Farkle.Grammars.GoldParser;

internal static class GoldGrammarReader
{
    private static void ValidateHeader(string header)
    {
        string? errorMessage = header switch
        {
            GrammarConstants.HeaderMagicString => "Grammar files produced by Farkle 7.x. must be opened with the Grammar.Create method instead.",
            GrammarConstants.EgtNeoHeaderString => "EGTneo grammar files produced by Farkle 6.x are not supported.",
            GrammarConstants.Egt5HeaderString => null,
            GrammarConstants.CgtHeaderString => "CGT grammar files produced by GOLD Parser are not supported.",
            _ => "Unrecognized file format."
        };

        if (errorMessage is not null)
        {
            ThrowHelpers.ThrowNotSupportedException(errorMessage);
        }
    }

    private static void AssignOnce<T>(ImmutableArray<T>[] array, int index, ImmutableArray<T> item)
    {
        if (!array[index].IsDefault)
        {
            ThrowHelpers.ThrowInvalidDataException();
        }

        array[index] = item;
    }

    private static void AssignOnce<T>(T[] array, int index, T item)
    {
        if (array[index] is not null)
        {
            ThrowHelpers.ThrowInvalidDataException();
        }

        array[index] = item;
    }

    private static void DecrementRemainingRecord(ref int recordCount)
    {
        if (recordCount == 0)
        {
            ThrowHelpers.ThrowInvalidDataException();
        }

        recordCount--;
    }

    private static ushort InferStartSymbol(ImmutableArray<LalrAction>[] lalrStates)
    {
        var firstState = lalrStates[0];
        for (int i = 0; i < firstState.Length; i++)
        {
            if (firstState[i].Kind == LalrActionKind.Goto)
            {
                int candidateStateIndex = firstState[i].TargetIndex;
                foreach (ref readonly var action in lalrStates[candidateStateIndex].AsSpan())
                {
                    if (action.Kind == LalrActionKind.Accept)
                    {
                        return (ushort)candidateStateIndex;
                    }
                }
            }
        }

        ThrowHelpers.ThrowInvalidDataException();
        return 0;
    }

    static ushort ReadIndex(this GrammarBinaryReader reader, int arrayLength)
    {
        ushort index = reader.ReadUInt16();
        if (index >= arrayLength)
        {
            ThrowHelpers.ThrowInvalidDataException();
        }
        return index;
    }

    static ImmutableArray<ushort> ReadIndexArray(this GrammarBinaryReader reader, int count, int arrayLength)
    {
        if (count != reader.RemainingEntries)
        {
            ThrowHelpers.ThrowInvalidDataException();
        }

        var builder = ImmutableArray.CreateBuilder<ushort>(count);
        for (int i = 0; i < count; i++)
        {
            builder.Add(reader.ReadIndex(arrayLength));
        }
        return builder.MoveToImmutable();
    }

    public static GoldGrammar ReadGrammar(Stream stream)
    {
        GrammarBinaryReader reader = new GrammarBinaryReader(stream);
        ValidateHeader(reader.Header);

        string? grammarName = null;
        ImmutableArray<(char Start, char End)>[]? characterSets = null;
        Symbol[]? symbols = null;
        GoldGrammar.Group[]? groups = null;
        GoldGrammar.Production[]? productions = null;
        DfaState[]? dfaStates = null;
        ImmutableArray<LalrAction>[]? lalrStates = null;

        int remainingCharacterSets = 0, remainingSymbols = 0, remainingGroups = 0, remainingProductions = 0, remainingDfaStates = 0, remainingLalrStates = 0;

        while (reader.NextRecord())
        {
            byte entryKind = reader.ReadByte();
            switch ((char)entryKind)
            {
                case 'p':
                    {
                        int index = reader.ReadUInt16();
                        _ = reader.ReadString();
                        string value = reader.ReadString();
                        if (index == 0)
                        {
                            if (grammarName is null)
                            {
                                ThrowHelpers.ThrowInvalidDataException();
                            }
                            grammarName = value;
                        }
                    }
                    break;
                case 't' when characterSets is null:
                    {
                        Debug.Assert(symbols is null && groups is null && productions is null && dfaStates is null && lalrStates is null);
                        CreateArray(ref symbols, remainingSymbols = reader.ReadUInt16());
                        CreateArray(ref characterSets, remainingCharacterSets = reader.ReadUInt16());
                        CreateArray(ref productions, remainingProductions = reader.ReadUInt16());
                        CreateArray(ref dfaStates, remainingDfaStates = reader.ReadUInt16());
                        CreateArray(ref lalrStates, remainingLalrStates = reader.ReadUInt16());
                        CreateArray(ref groups, remainingGroups = reader.ReadUInt16());

                        static void CreateArray<T>([NotNull] ref T[]? array, int length) => array = new T[length];
                    }
                    break;
                case 'c' when characterSets is not null:
                    {
                        DecrementRemainingRecord(ref remainingCharacterSets);
                        int index = reader.ReadIndex(characterSets.Length);
                        _ = reader.ReadUInt16();
                        int rangeCount = reader.ReadUInt16();
                        reader.SkipEntry();

                        if (rangeCount != reader.RemainingEntries / 2)
                        {
                            ThrowHelpers.ThrowInvalidDataException();
                        }

                        var builder = ImmutableArray.CreateBuilder<(char, char)>(rangeCount);
                        for (int i = 0; i < rangeCount; i++)
                        {
                            char cFrom = (char)reader.ReadUInt16();
                            char cTo = (char)reader.ReadUInt16();
                            builder.Add((cFrom, cTo));
                        }

                        AssignOnce(characterSets, index, builder.MoveToImmutable());
                    }
                    break;
                case 'S' when symbols is not null && groups is null:
                    {
                        DecrementRemainingRecord(ref remainingSymbols);
                        int index = reader.ReadIndex(symbols.Length);
                        string name = reader.ReadString();
                        SymbolKind symbolKind = reader.ReadUInt16() switch
                        {
                            0 => SymbolKind.Nonterminal,
                            1 => SymbolKind.Terminal,
                            2 => SymbolKind.Noise,
                            3 => SymbolKind.EndOfFile,
                            4 => SymbolKind.GroupStart,
                            5 => SymbolKind.GroupEnd,
                            7 => SymbolKind.Error,
                            _ => throw new InvalidDataException()
                        };

                        AssignOnce(symbols, index, new() { Kind = symbolKind, Name = name });
                    }
                    break;
                case 'g' when groups is not null:
                    {
                        Debug.Assert(symbols is not null);
                        DecrementRemainingRecord(ref remainingGroups);
                        int index = reader.ReadIndex(groups.Length);
                        ref var slot = ref groups[index];
                        string name = reader.ReadString();
                        ushort containerIndex = reader.ReadIndex(symbols.Length);
                        ushort startIndex = reader.ReadIndex(symbols.Length);
                        ushort endIndex = reader.ReadIndex(symbols.Length);
                        bool advanceByChar = reader.ReadUInt16() != 0;
                        bool keepEndToken = reader.ReadUInt16() == 0;
                        reader.SkipEntry();
                        int nestingCount = reader.ReadUInt16();
                        ImmutableArray<ushort> nesting = reader.ReadIndexArray(nestingCount, symbols.Length);

                        AssignOnce(groups, index, new()
                        {
                            Name = name,
                            ContainerIndex = containerIndex,
                            StartIndex = startIndex,
                            EndIndex = endIndex,
                            AdvanceByChar = advanceByChar,
                            KeepEndToken = keepEndToken,
                            Nesting = nesting
                        });
                    }
                    break;
                case 'R' when productions is not null:
                    {
                        Debug.Assert(symbols is not null);
                        DecrementRemainingRecord(ref remainingProductions);
                        int index = reader.ReadUInt16();
                        ushort headIndex = reader.ReadUInt16();
                        reader.SkipEntry();
                        ImmutableArray<ushort> members = reader.ReadIndexArray(reader.RemainingEntries, symbols.Length);

                        AssignOnce(productions, index, new() { HeadIndex = headIndex, Members = members });
                    }
                    break;
                case 'I':
                    {
                        ushort dfaStateCount = reader.ReadUInt16();
                        ushort lalrStateCount = reader.ReadUInt16();
                        if ((dfaStateCount, lalrStateCount) is not (0, 0))
                        {
                            ThrowHelpers.ThrowNotSupportedException("Grammars whose initial DFA or LALR states are not the first ones are not supported. Please open a GitHub issue if such grammar files exist.");
                        }
                    }
                    break;
                case 'D' when dfaStates is not null:
                    {
                        Debug.Assert(characterSets is not null && symbols is not null);
                        DecrementRemainingRecord(ref remainingDfaStates);
                        int index = reader.ReadIndex(dfaStates.Length);
                        bool isAccept = reader.ReadBoolean();
                        ushort acceptIndex = reader.ReadUInt16();
                        reader.SkipEntry();

                        int edgeCount = Math.DivRem(reader.RemainingEntries, 3, out int leftoverEntries);

                        if (leftoverEntries != 0)
                        {
                            ThrowHelpers.ThrowInvalidDataException();
                        }

                        var builder = ImmutableArray.CreateBuilder<(ushort, ushort)>(edgeCount);
                        for (int i = 0; i < edgeCount; i++)
                        {
                            ushort charSetIndex = reader.ReadIndex(characterSets.Length);
                            ushort targetIndex = reader.ReadIndex(symbols.Length);
                            reader.SkipEntry();

                            builder.Add((charSetIndex, targetIndex));
                        }

                        AssignOnce(dfaStates, index, new() { AcceptIndex = isAccept ? acceptIndex : null, Edges = builder.MoveToImmutable() });
                    }
                    break;
                case 'L' when lalrStates is not null:
                    {
                        Debug.Assert(symbols is not null && productions is not null);
                        DecrementRemainingRecord(ref remainingLalrStates);
                        int index = reader.ReadIndex(lalrStates.Length);
                        reader.SkipEntry();

                        int actionCount = Math.DivRem(reader.RemainingEntries, 4, out int leftoverEntries);
                        if (leftoverEntries != 0)
                        {
                            ThrowHelpers.ThrowInvalidDataException();
                        }

                        var builder = ImmutableArray.CreateBuilder<LalrAction>(actionCount);
                        for (int i = 0; i < actionCount; i++)
                        {
                            ushort symbolIndex = reader.ReadIndex(symbols.Length);
                            LalrActionKind actionType;
                            ushort targetIndex;
                            switch (reader.ReadUInt16())
                            {
                                case 1:
                                    actionType = LalrActionKind.Shift;
                                    targetIndex = reader.ReadIndex(lalrStates.Length);
                                    break;
                                case 2:
                                    actionType = LalrActionKind.Reduce;
                                    targetIndex = reader.ReadIndex(productions.Length);
                                    break;
                                case 3:
                                    actionType = LalrActionKind.Accept;
                                    targetIndex = 0;
                                    break;
                                case 4:
                                    actionType = LalrActionKind.Goto;
                                    targetIndex = reader.ReadIndex(lalrStates.Length);
                                    break;
                                default:
                                    throw new InvalidDataException();
                            }
                            reader.SkipEntry();

                            builder.Add(new() { SymbolIndex = symbolIndex, Kind = actionType, TargetIndex = targetIndex });
                        }
                        AssignOnce(lalrStates, index, builder.MoveToImmutable());
                    }
                    break;
                default:
                    ThrowHelpers.ThrowInvalidDataException();
                    break;
            }
        }

        if (grammarName is null
            || characterSets is null
            || (remainingSymbols | remainingCharacterSets | remainingProductions | remainingDfaStates | remainingLalrStates | remainingGroups) != 0)
        {
            ThrowHelpers.ThrowInvalidDataException();
        }

        Debug.Assert(symbols is not null && groups is not null && productions is not null && dfaStates is not null && lalrStates is not null);

        return new()
        {
            Name = grammarName,
            StartSymbol = InferStartSymbol(lalrStates),
            CharacterSets = characterSets,
            Symbols = symbols,
            Groups = groups,
            Productions = productions,
            DfaStates = dfaStates,
            LalrStates = lalrStates
        };
    }
}
