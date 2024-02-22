// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;

namespace Farkle.Builder;

/// <summary>
/// Contains options to customize the creation of a grammar.
/// </summary>
/// <remarks>
/// Unlike <see cref="BuilderOptions"/>, these options can and do change the
/// resulting grammar.
/// </remarks>
internal readonly struct GrammarGlobalOptions
{
    public string? GrammarName { get; init; } = null;

    public CaseSensitivity CaseSensitivity { get; init; } = CaseSensitivity.CaseSensitive;

    public bool AutoWhitespace { get; init; } = true;

    public bool? NewLineIsNoisy { get; init; } = null;

    public ImmutableList<(string Name, Regex Regex)> NoiseSymbols { get; init; } = [];

    public ImmutableList<(string Start, string? EndOrNewLine)> Comments { get; init; } = [];

    public GrammarGlobalOptions() { }

    public static readonly GrammarGlobalOptions Default = new();

    public GrammarGlobalOptions AddNoiseSymbol(string name, Regex regex) =>
        this with { NoiseSymbols = NoiseSymbols.Add((name, regex)) };

    public GrammarGlobalOptions AddBlockComment(string start, string end) =>
        this with { Comments = Comments.Add((start, end)) };

    public GrammarGlobalOptions AddLineComment(string start) =>
        this with { Comments = Comments.Add((start, null)) };
}
