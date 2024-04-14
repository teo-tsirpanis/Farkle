// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder.Dfa;
using Farkle.Builder.Lr;
using Farkle.Diagnostics.Builder;
using Farkle.Grammars;
using Farkle.Grammars.Writers;

namespace Farkle.Builder;

/// <summary>
/// Contains the logic to convert from <see cref="GrammarDefinition"/> to <see cref="Grammar"/>
/// objects.
/// </summary>
internal static class GrammarBuild
{
    private static readonly Regex NewLineRegex = Regex.Choice([Regex.OneOf(['\n', '\r']), Regex.Literal("\r\n")]);

    private static readonly Regex WhitespaceRegex = Regex.OneOf(['\t', '\n', '\r', ' ']).AtLeast(1);

    private static readonly Regex WhitespaceNoNewLineRegex = Regex.OneOf(['\t', ' ']).AtLeast(1);

    private static TokenSymbolAttributes GetTerminalFlags(ISymbolBase symbol, out bool hasSpecialName)
    {
        switch (symbol)
        {
            case Terminal { Options: var options }:
                return MapFlags((uint)options, (uint)TerminalOptions.Hidden, (uint)TerminalOptions.Noisy,
                    (uint)TerminalOptions.SpecialName, out hasSpecialName);
            case VirtualTerminal { Options: var options }:
                return MapFlags((uint)options, (uint)TerminalOptions.Hidden, (uint)TerminalOptions.Noisy,
                    (uint)TerminalOptions.SpecialName, out hasSpecialName);
            case Group { Options: var options }:
                return MapFlags((uint)options, (uint)GroupOptions.Hidden, (uint)GroupOptions.Noisy,
                    (uint)GroupOptions.SpecialName, out hasSpecialName);
            default:
                hasSpecialName = false;
                return TokenSymbolAttributes.None;
        }

        static TokenSymbolAttributes MapFlags(uint flags, uint hiddenFlag, uint noisyFlag, uint specialNameFlag, out bool hasSpecialName)
        {
            hasSpecialName = (flags & specialNameFlag) != 0;
            return ((flags & hiddenFlag) != 0 ? TokenSymbolAttributes.Hidden : TokenSymbolAttributes.None)
                | ((flags & noisyFlag) != 0 ? TokenSymbolAttributes.Noise : TokenSymbolAttributes.None)
                | TokenSymbolAttributes.Terminal;
        }
    }

    public static Grammar Build(GrammarDefinition grammarDefinition, BuilderOptions options)
    {
        ref readonly GrammarGlobalOptions globalOptions = ref grammarDefinition.GlobalOptions;
        bool autoWhitespace = globalOptions.AutoWhitespace;
        bool newLineIsNoisy = globalOptions.NewLineIsNoisy ?? autoWhitespace;
        bool literalsCaseInsensitive = globalOptions.CaseSensitivity is not CaseSensitivity.CaseSensitive;
        var operatorScope = globalOptions.OperatorScope;
        var log = options.Log;
        var writer = new GrammarWriter();
        bool isUnparsable = false;

        // Maps symbol handles (terminals or productions) to their representation
        // in the operator scope. We create and populate this only if needed.
        var operatorSymbolMap = operatorScope is not null ? new Dictionary<EntityHandle, object>() : null;

        var productionMemberMap = new Dictionary<ISymbolBase, EntityHandle>(
            grammarDefinition.Terminals.Count + grammarDefinition.Nonterminals.Count,
            grammarDefinition.SymbolIdentityObjectComparer);
        Dictionary<string, EntityHandle>? specialNameMap = null;
        bool writeSpecialNames = true;

        // Add terminals.
        GrammarSymbolsProvider dfaSymbols = new(grammarDefinition.Terminals.Count);
        // We must add the groups' start and end symbols after the terminals.
        // Keep the groups in this list to process them later.
        List<Group>? groups = null;
        // NewLine might appear as either a terminal, or the end of a line group.
        // Keep it here if it is encountered to reuse the symbol in the grammar.
        TokenSymbolHandle newLineHandle = default;
        foreach (ISymbolBase terminal in grammarDefinition.Terminals)
        {
            string name = grammarDefinition.GetName(terminal);
            TokenSymbolAttributes flags = GetTerminalFlags(terminal, out bool hasSpecialName);
            if (terminal is NewLine && newLineIsNoisy)
            {
                flags |= TokenSymbolAttributes.Noise;
            }
            TokenSymbolHandle handle = writer.AddTokenSymbol(writer.GetOrAddString(name), flags);
            productionMemberMap.Add(terminal, handle);
            if (GetTerminalRegex(terminal) is { } regex)
            {
                dfaSymbols.Add(regex, handle, name, TokenSymbolKind.Terminal);
            }
            if (terminal is NewLine)
            {
                newLineHandle = handle;
            }
            if (terminal is Group group)
            {
                groups ??= [];
                groups.Add(group);
            }
            if (hasSpecialName)
            {
                // Add the symbol's _original_ name as a special name.
                specialNameMap ??= [];
                if (!specialNameMap.TryAdd(terminal.Name, handle))
                {
                    log.DuplicateSpecialName(terminal.Name);
                    // If there is a duplicate special name, no special names will be written
                    // to the grammar, and the grammar will be marked as unparsable.
                    isUnparsable = true;
                    writeSpecialNames = false;
                }
            }
            operatorSymbolMap?.Add(handle, terminal);
        }

        if (writeSpecialNames && specialNameMap is not null)
        {
            foreach (var kvp in specialNameMap)
            {
                writer.AddSpecialName(writer.GetOrAddString(kvp.Key), kvp.Value);
            }
        }

        // Add groups.
        if (groups is not null)
        {
            foreach (Group group in groups)
            {
                string name = grammarDefinition.GetName(group);
                (string groupStart, string? groupEndOrNewLine) = group switch
                {
                    LineGroup x => (x.GroupStart, null),
                    BlockGroup x => (x.GroupStart, x.GroupEnd),
                    _ => throw new NotSupportedException()
                };
                TokenSymbolHandle container = (TokenSymbolHandle)productionMemberMap[group];
                TokenSymbolHandle startHandle = writer.AddTokenSymbol(writer.GetOrAddString(groupStart), TokenSymbolAttributes.GroupStart);
                dfaSymbols.Add(Regex.Literal(groupStart), startHandle, groupStart, TokenSymbolKind.GroupStart);
                TokenSymbolHandle endHandle;
                GroupAttributes flags;
                if (groupEndOrNewLine is null)
                {
                    endHandle = GetOrCreateNewLineForGroupEnd();
                    flags = GroupAttributes.AdvanceByCharacter | GroupAttributes.EndsOnEndOfInput | GroupAttributes.KeepEndToken;
                }
                else
                {
                    endHandle = writer.AddTokenSymbol(writer.GetOrAddString(groupEndOrNewLine), TokenSymbolAttributes.None);
                    dfaSymbols.Add(Regex.Literal(groupEndOrNewLine), endHandle, groupEndOrNewLine, TokenSymbolKind.GroupEnd);
                    flags = GroupAttributes.AdvanceByCharacter;
                }
                bool isRecursive = (group.Options & GroupOptions.Recursive) != 0;
                uint groupIndex = writer.AddGroup(writer.GetOrAddString(name), container, flags, startHandle, endHandle, isRecursive ? 1 : 0);
                if (isRecursive)
                {
                    writer.AddGroupNesting(groupIndex);
                }
            }
        }

        // Add nonterminals.
        foreach (INonterminal nonterminal in grammarDefinition.Nonterminals)
        {
            string name = grammarDefinition.GetName(nonterminal);
            int productionCount = nonterminal.FreezeAndGetProductions().Length;
            NonterminalHandle handle = writer.AddNonterminal(writer.GetOrAddString(name), NonterminalAttributes.None, productionCount);
            productionMemberMap.Add(nonterminal, handle);
        }

        // Add productions.
        // Keep a flattened list of production members; it will be needed by the syntax provider.
        List<EntityHandle> productionMembers = [];
        foreach (IProduction production in grammarDefinition.Productions)
        {
            ProductionHandle handle = writer.AddProduction(production.Members.Length);
            foreach (IGrammarSymbol member in production.Members)
            {
                EntityHandle memberHandle = productionMemberMap[member.Symbol];
                productionMembers.Add(memberHandle);
                writer.AddProductionMember(memberHandle);
            }
            if (production.PrecedenceToken is { } precedenceToken)
            {
                operatorSymbolMap?.Add(handle, precedenceToken);
            }
        }

        // Add comments.
        if (globalOptions.Comments is { Count: > 0 } comments)
        {
            TokenSymbolHandle commentSymbol = writer.AddTokenSymbol(writer.GetOrAddString("Comment"), TokenSymbolAttributes.Noise);
            foreach ((string start, string? endOrNewLine) in comments)
            {
                TokenSymbolHandle groupStart = writer.AddTokenSymbol(writer.GetOrAddString(start), TokenSymbolAttributes.GroupStart);
                dfaSymbols.Add(Regex.Literal(start), groupStart, start, TokenSymbolKind.GroupStart);
                TokenSymbolHandle groupEnd;
                GroupAttributes flags;
                if (endOrNewLine is null)
                {
                    groupEnd = GetOrCreateNewLineForGroupEnd();
                    flags = GroupAttributes.AdvanceByCharacter | GroupAttributes.EndsOnEndOfInput | GroupAttributes.KeepEndToken;
                }
                else
                {
                    groupEnd = writer.AddTokenSymbol(writer.GetOrAddString(endOrNewLine), TokenSymbolAttributes.None);
                    dfaSymbols.Add(Regex.Literal(endOrNewLine), groupEnd, endOrNewLine, TokenSymbolKind.GroupEnd);
                    flags = GroupAttributes.AdvanceByCharacter;
                }
                string name = endOrNewLine is null ? "Comment Line" : "Comment Block";
                writer.AddGroup(writer.GetOrAddString(name), commentSymbol, flags, groupStart, groupEnd, 0);
            }
        }

        // Add whitespace.
        if (autoWhitespace)
        {
            // If a NewLine symbol exists, the whitespace regex will be only spaces and tabs.
            Regex whitespaceRegex = newLineHandle.HasValue ? WhitespaceNoNewLineRegex : WhitespaceRegex;
            const string WhitespaceName = "Whitespace";
            TokenSymbolHandle whitespaceHandle = writer.AddTokenSymbol(writer.GetOrAddString(WhitespaceName),
                TokenSymbolAttributes.Noise | TokenSymbolAttributes.Generated);
            dfaSymbols.Add(whitespaceRegex, whitespaceHandle, WhitespaceName, TokenSymbolKind.Noise);
        }

        // Add miscellaneous noise symbols.
        foreach ((string name, Regex regex) in globalOptions.NoiseSymbols)
        {
            TokenSymbolHandle handle = writer.AddTokenSymbol(writer.GetOrAddString(name), TokenSymbolAttributes.Noise);
            dfaSymbols.Add(regex, handle, name, TokenSymbolKind.Noise);
        }

        // TODO: Add conflict resolver based on the operator scope.

        // Build state machines.
        bool isCaseSensitive = globalOptions.CaseSensitivity is not CaseSensitivity.CaseInsensitive;
        var dfaWriter = DfaBuild<char>.Build(dfaSymbols, isCaseSensitive, true, options.MaxTokenizerStates, log, options.CancellationToken);
        if (dfaWriter is not null)
        {
            writer.AddStateMachine(dfaWriter);
        }

        var conflictResolver = operatorScope is not null
            ? new OperatorScopeConflictResolver(operatorScope, operatorSymbolMap!, literalsCaseInsensitive, log)
            : null;
        var syntaxProvider = new GrammarSyntaxProvider(grammarDefinition, productionMembers);
        writer.AddStateMachine(LalrBuild.Build(syntaxProvider, conflictResolver, log, options.CancellationToken));

        // Set grammar info.
        string grammarName = globalOptions.GrammarName ?? grammarDefinition.GetName(grammarDefinition.StartSymbol);
        GrammarAttributes attributes = isUnparsable ? GrammarAttributes.Unparsable : GrammarAttributes.None;
        NonterminalHandle startSymbol = (NonterminalHandle)productionMemberMap[grammarDefinition.StartSymbol];
        writer.SetGrammarInfo(writer.GetOrAddString(grammarName), startSymbol, attributes);

        return Grammar.Create(writer.ToImmutableArray());

        Regex? GetTerminalRegex(ISymbolBase symbol)
        {
            return symbol switch
            {
                Terminal terminal => terminal.Regex,
                Literal literal when literalsCaseInsensitive => Regex.Literal(literal.Value).CaseInsensitive(),
                Literal literal => Regex.Literal(literal.Value),
                NewLine => NewLineRegex,
                _ => null
            };
        }

        TokenSymbolHandle GetOrCreateNewLineForGroupEnd()
        {
            if (!newLineHandle.HasValue)
            {
                string name = NewLine.Instance.Name;
                newLineHandle = writer.AddTokenSymbol(writer.GetOrAddString(name),
                    autoWhitespace ? TokenSymbolAttributes.Noise : TokenSymbolAttributes.None);
                dfaSymbols.Add(NewLineRegex, newLineHandle, name,
                    autoWhitespace ? TokenSymbolKind.Noise : TokenSymbolKind.GroupEnd);
            }
            return newLineHandle;
        }
    }

    private sealed class GrammarSymbolsProvider(int sizeHint) : IGrammarSymbolsProvider
    {
        private readonly List<(Regex Regex, TokenSymbolHandle Handle, string Name, TokenSymbolKind Kind)> _symbols = new(sizeHint);

        private readonly Dictionary<string, int> _symbolKinds = new(sizeHint);

        private bool ShouldDisambiguate(string name) =>
            _symbolKinds.TryGetValue(name, out int kind) && !BitOperationsCompat.IsPow2(kind);

        public void Add(Regex regex, TokenSymbolHandle handle, string name, TokenSymbolKind kind)
        {
            _symbols.Add((regex, handle, name, kind));
            if (_symbolKinds.TryGetValue(name, out int existingKind))
            {
                _symbolKinds[name] = existingKind | (1 << (int)kind);
            }
            else
            {
                _symbolKinds[name] = 1 << (int)kind;
            }
        }

        public int SymbolCount => _symbols.Count;

        public Regex GetRegex(int index) => _symbols[index].Regex;

        public TokenSymbolHandle GetTokenSymbolHandle(int index) => _symbols[index].Handle;

        public BuilderSymbolName GetName(int index)
        {
            (_, _, string name, TokenSymbolKind kind) = _symbols[index];
            return new(name, kind, ShouldDisambiguate(name));
        }
    }

    private sealed class GrammarSyntaxProvider : IGrammarSyntaxProvider
    {
        private readonly GrammarDefinition _grammarDefinition;

        private readonly (int FirstProduction, int ProductionCount)[] _nonterminalProductionBounds;

        private readonly int[] _productionHeads;

        private readonly (int FirstMember, int MemberCount)[] _productionMemberBounds;

        private readonly List<EntityHandle> _productionMembers;

        public GrammarSyntaxProvider(GrammarDefinition grammarDefinition, List<EntityHandle> productionMembers)
        {
            _grammarDefinition = grammarDefinition;
            _nonterminalProductionBounds = new (int, int)[grammarDefinition.Nonterminals.Count];
            _productionHeads = new int[grammarDefinition.Productions.Count];
            _productionMemberBounds = new (int, int)[grammarDefinition.Productions.Count];
            _productionMembers = productionMembers;

            int productionIndex = 0;
            for (int i = 0; i < _nonterminalProductionBounds.Length; i++)
            {
                int productionCount = _grammarDefinition.Nonterminals[i].FreezeAndGetProductions().Length;
                _nonterminalProductionBounds[i] = (productionIndex, productionCount);
                _productionHeads.AsSpan(productionIndex, productionCount).Fill(i);
                productionIndex += productionCount;
            }
        }

        public int TerminalCount => _grammarDefinition.Terminals.Count;

        public int NonterminalCount => _grammarDefinition.Nonterminals.Count;

        public int ProductionCount => _grammarDefinition.Productions.Count;

        public int StartSymbol => 0;

        public string GetTerminalName(int index) => _grammarDefinition.GetName(_grammarDefinition.Terminals[index]);

        public string GetNonterminalName(int index) => _grammarDefinition.GetName(_grammarDefinition.Nonterminals[index]);

        public (int FirstProduction, int ProductionCount) GetNonterminalProductions(int index) => _nonterminalProductionBounds[index];

        public int GetProductionHead(int index) => _productionHeads[index];

        public (int FirstMember, int MemberCount) GetProductionMembers(int index) => _productionMemberBounds[index];

        public (int SymbolIndex, bool IsTerminal) GetProductionMember(int index)
        {
            EntityHandle member = _productionMembers[index];
            return ((int)(member.TableIndex - 1), member.Kind == TableKind.TokenSymbol);
        }
    }
}
