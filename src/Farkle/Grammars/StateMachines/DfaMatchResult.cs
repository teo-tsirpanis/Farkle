// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Represents the result of a DFA matching operation.
/// </summary>
internal readonly struct DfaMatchResult
{
    private readonly uint _acceptSymbol;

    private const uint NeedsMoreCharsMask = 0x80000000;

    /// <summary>
    /// Whether the DFA failed to examine the whole input because it needed more characters.
    /// </summary>
    /// <remarks>
    /// If set to <see langword="true"/>, the properties of this type refer to the last time
    /// the DFA accepted a symbol, if it did.
    /// </remarks>
    public bool NeedsMoreChars => (_acceptSymbol & NeedsMoreCharsMask) != 0;

    public TokenSymbolHandle AcceptSymbol => new(_acceptSymbol & ~NeedsMoreCharsMask);

    public int CharactersRead { get; }

    public int DfaState { get; }

    private DfaMatchResult(uint acceptSymbol, int state, int idx)
    {
        _acceptSymbol = acceptSymbol;
        CharactersRead = idx;
        DfaState = state;
    }

    public static DfaMatchResult CreateSuccess(TokenSymbolHandle acceptSymbol, int dfaState, int tokenLength) =>
        new(acceptSymbol.TableIndex, dfaState, tokenLength);

    public static DfaMatchResult CreateError(int dfaState, int charactersRead) =>
        new(0, dfaState, charactersRead);

    public static DfaMatchResult CreateNeedsMoreChars(TokenSymbolHandle lastAcceptSymbol, int lastAcceptState, int lastAcceptPosition) =>
        new(lastAcceptSymbol.TableIndex | NeedsMoreCharsMask, lastAcceptState, lastAcceptPosition);
}
