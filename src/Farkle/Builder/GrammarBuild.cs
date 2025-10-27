// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using Farkle.Builder.Dfa;
using Farkle.Builder.Lr;
using Farkle.Diagnostics;
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
    private static readonly Regex NewLineRegex = Regex.Choice(Regex.OneOf('\n', '\r'), Regex.Literal("\r\n")).CaseSensitive();

    private static readonly Regex WhitespaceRegex = Regex.OneOf('\t', '\n', '\r', ' ').AtLeast(1).CaseSensitive();

    private static readonly Regex WhitespaceNoNewLineRegex = Regex.OneOf('\t', ' ').AtLeast(1).CaseSensitive();

    private static TokenSymbolAttributes GetTerminalFlags(ISymbolBase symbol)
    {
        return symbol switch
        {
            Terminal { Options: var options } =>
                MapFlags((uint)options, (uint)TerminalOptions.Hidden, (uint)TerminalOptions.Noisy),
            VirtualTerminal { Options: var options } =>
                MapFlags((uint)options, (uint)TerminalOptions.Hidden, (uint)TerminalOptions.Noisy),
            Group { Options: var options } =>
                MapFlags((uint)options, (uint)GroupOptions.Hidden, (uint)GroupOptions.Noisy),
            _ => TokenSymbolAttributes.Terminal,
        };

        static TokenSymbolAttributes MapFlags(uint flags, uint hiddenFlag, uint noisyFlag)
        {
            return ((flags & hiddenFlag) != 0 ? TokenSymbolAttributes.Hidden : TokenSymbolAttributes.None)
                | ((flags & noisyFlag) != 0 ? TokenSymbolAttributes.Noise : TokenSymbolAttributes.None)
                | TokenSymbolAttributes.Terminal;
        }
    }

    private static string ExtractFirstPossibleCharacters(Regex regex)
    {
        // Support only the subset of regexes the builder currently supports to start and end groups.
        // If we want to generalize groups in the future and have them be bounded by arbitrary regexes,
        // we might need to somehow run this inside the DFA builder.
        while (regex.IsAccept(out Regex? r, out _, out _))
        {
            regex = r;
        }
        if (regex == NewLineRegex)
        {
            return "\n\r";
        }
        if (regex.IsStringLiteral(out string? s))
        {
            return s[0].ToString();
        }
        ThrowHelpers.ThrowNotSupportedException();
        return default;
    }

    /// <summary>
    /// Creates a <see cref="Regex"/> that will be used to build a custom DFA
    /// for when the tokenizer is inside a group. If a custom DFA cannot be used,
    /// this function will return <see langword="null"/>.
    /// </summary>
    private static Regex? GetGroupRegex(string start, Regex endRegexWithAccept, bool isRecursive,
        GroupAttributes groupAttributes, out bool addEndRegexToMainDfa)
    {
        addEndRegexToMainDfa = true;
        if ((groupAttributes & GroupAttributes.AdvanceByCharacter) == 0)
        {
            // Token groups cannot use a custom DFA starting state by definition.
            return null;
        }
        // Get the list of characters that will immediately stop the DFA when inside the group.
        // If the group is not nested, all characters are allowed, otherwise it's the characters
        // that might start startRegex.
        ImmutableArray<char> prohibitedCharacters;
        if (isRecursive)
        {
            char c = start[0];
            prohibitedCharacters = [c];
        }
        else
        {
            prohibitedCharacters = [];
        }
        // If a group keeps its end token in the input, we must fail right when we might
        // encounter it, in order to let the main DFA precisely match it.
        bool keepEndToken = (groupAttributes & GroupAttributes.KeepEndToken) != 0;
        if (keepEndToken)
        {
            prohibitedCharacters = prohibitedCharacters.AddRange(ExtractFirstPossibleCharacters(endRegexWithAccept));
        }
        // Set HighPriorityInverted because, if a recursive group starts and ends with the same character,
        // we must fail and leave it to the main DFA to determine which of the two (or none) happened.
        // Set BreakOnAccept, in order to stop reading random text when the group end gets matched.
        Regex result = Regex.Chars(prohibitedCharacters, Regex.CharsFlags.HighPriorityInverted | Regex.CharsFlags.BreakOnAccept).ZeroOrMore();
        if (!keepEndToken)
        {
            result += endRegexWithAccept;
            addEndRegexToMainDfa = false;
        }
        return result;
    }

    /// <summary>
    /// Builds a <see cref="Grammar"/> object from a <see cref="GrammarDefinition"/>.
    /// </summary>
    /// <param name="grammarDefinition">The grammar definition.</param>
    /// <param name="artifacts">The artifacts to build. Only <see cref="BuilderArtifacts.GrammarLrStateMachine"/>
    /// and <see cref="BuilderArtifacts.GrammarDfaOnChar"/> are considered.</param>
    /// <param name="options">Options to control the building process.</param>
    /// <param name="errors">An optional collection to store diagnostics of
    /// severity <see cref="DiagnosticSeverity.Error"/>.</param>
    public static Grammar Build(GrammarDefinition grammarDefinition, BuilderArtifacts artifacts, BuilderOptions options, ICollection<BuilderDiagnostic>? errors = null)
    {
        var log = options.Log.WithRedirectErrors(errors);
        string grammarName = grammarDefinition.GrammarName;
        log.InformationLocalized(nameof(Resources.Builder_BuildingStarted), grammarName);
        Grammar grammar = Build(grammarDefinition, artifacts, options, in log);
        // Get conflicts and log them. Skip the computation if no errors are logged
        // (i.e. the log has no listeners at all).
        if (log.IsEnabled(DiagnosticSeverity.Error))
        {
            foreach (LrConflict conflict in LrConflict.GetConflicts(grammar))
            {
                log.LrConflict(conflict);
            }
        }
        log.InformationLocalized(nameof(Resources.Builder_BuildingFinished), grammarName, grammar.TokenSymbols.Count,
            grammar.Nonterminals.Count, grammar.Productions.Count, grammar.LrStateMachine?.Count ?? 0, grammar.DfaOnChar?.Count ?? 0);
        return grammar;
    }

    private static Grammar Build(GrammarDefinition grammarDefinition, BuilderArtifacts artifacts, BuilderOptions options, in BuilderLogger log)
    {
        ref readonly GrammarGlobalOptions globalOptions = ref grammarDefinition.GlobalOptions;
        bool autoWhitespace = globalOptions.AutoWhitespace;
        bool newLineIsNoisy = globalOptions.NewLineIsNoisy ?? autoWhitespace;
        bool literalsCaseInsensitive = globalOptions.CaseSensitivity is not CaseSensitivity.CaseSensitive;
        var operatorScope = globalOptions.OperatorScope;
        var writer = new GrammarWriter();

        // Maps symbol handles (terminals or productions) to their representation
        // in the operator scope. We create and populate this only if needed.
        var operatorSymbolMap = operatorScope is not null ? new Dictionary<EntityHandle, object>() : null;

        // Maps builder identity objects of symbols to their entity handles.
        // They are used to get the handle of production members, and also to
        // make sure some literals
        // The keys must be obtained from the GrammarDefinition.GetSymbolIdentityObject method,
        // unless we know for sure that the symbol is not a literal, in which case we can directly
        // pass the ISymbolBase object.
        var symbolMap = new Dictionary<object, EntityHandle>(
            grammarDefinition.Terminals.Count + grammarDefinition.Nonterminals.Count,
            grammarDefinition.SymbolIdentityObjectComparer);

        // Add terminals.
        SymbolNameProvider? dfaSymbols = null;
        ImmutableArray<Regex>.Builder? regexBuilder = null;
        if ((artifacts & BuilderArtifacts.GrammarDfaOnChar) != 0)
        {
            dfaSymbols = new(grammarDefinition.Terminals.Count);
            regexBuilder = ImmutableArray.CreateBuilder<Regex>(grammarDefinition.Terminals.Count);
        }
        // We must add the groups' start and end symbols after the terminals.
        // Keep the groups in this list to process them later.
        List<Group>? groups = null;
        // NewLine might appear as either a terminal, or the end of a line group.
        // Keep it here if it is encountered to reuse the symbol in the grammar.
        TokenSymbolHandle newLineHandle = default;
        foreach (ISymbolBase terminal in grammarDefinition.Terminals)
        {
            string name = terminal.Name;
            TokenSymbolAttributes flags = GetTerminalFlags(terminal);
            if (terminal is NewLine && newLineIsNoisy)
            {
                flags |= TokenSymbolAttributes.Noise;
            }
            if (GrammarDefinition.IsGenerated(terminal))
            {
                flags |= TokenSymbolAttributes.Generated;
            }
            TokenSymbolHandle handle = writer.AddTokenSymbol(writer.GetOrAddString(name), flags);
            symbolMap.Add(GrammarDefinition.GetSymbolIdentityObject(terminal), handle);
            dfaSymbols?.Add(handle, name, TokenSymbolKind.Terminal);
            if (GetTerminalRegex(terminal) is { } regex)
            {
                regexBuilder?.Add(Regex.Accept(regex, handle, lowestPriority: false));
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
            operatorSymbolMap?.Add(handle, terminal);
        }

        // Add groups.
        int groupCount = groups?.Count ?? 0 + grammarDefinition.GlobalOptions.Comments?.Count ?? 0;
        List<Regex?>? groupDfaRegexes = null;
        if (options.EmitGroupOptimizedDfa && (artifacts & BuilderArtifacts.GrammarDfaOnChar) != 0 && groupCount > 0)
        {
            groupDfaRegexes = new List<Regex?>(groupCount);
        }
        if (groups is not null)
        {
            foreach (Group group in groups)
            {
                string? groupEndOrNewLine = group switch
                {
                    BlockGroup g => g.GroupEnd,
                    LineGroup => null,
                    _ => throw new NotSupportedException(),
                };
                TokenSymbolHandle container = (TokenSymbolHandle)symbolMap[group];
                HandleGroup(group.Name, group.GroupStart, groupEndOrNewLine, group.Options, container);
            }
        }

        // Add nonterminals.
        foreach (INonterminal nonterminal in grammarDefinition.Nonterminals)
        {
            string name = nonterminal.Name;
            NonterminalAttributes flags = NonterminalAttributes.None;
            if (GrammarDefinition.IsGenerated(nonterminal))
            {
                flags |= NonterminalAttributes.Generated;
            }
            int productionCount = nonterminal.FreezeAndGetProductions().Length;
            NonterminalHandle handle = writer.AddNonterminal(writer.GetOrAddString(name), flags, productionCount);
            symbolMap.Add(nonterminal, handle);
        }

        // Add productions.
        // Keep a flattened list of production members; it will be needed by the syntax provider.
        List<EntityHandle> productionMembers = [];
        foreach (IProduction production in grammarDefinition.Productions)
        {
            ProductionHandle handle = writer.AddProduction(production.Members.Length);
            foreach (IGrammarSymbol member in production.Members)
            {
                EntityHandle memberHandle = symbolMap[GrammarDefinition.GetSymbolIdentityObject(member.Symbol)];
                productionMembers.Add(memberHandle);
                writer.AddProductionMember(memberHandle);
            }
            operatorSymbolMap?.Add(handle, production);
        }

        // Add special names.
        foreach (var kvp in grammarDefinition.SpecialNames)
        {
            writer.AddSpecialName(writer.GetOrAddString(kvp.Key), symbolMap[kvp.Value]);
        }

        // Add comments.
        if (globalOptions.Comments is { Count: > 0 } comments)
        {
            TokenSymbolHandle commentSymbol = writer.AddTokenSymbol(writer.GetOrAddString("Comment"), TokenSymbolAttributes.Noise);
            foreach ((string start, string? endOrNewLine) in comments)
            {
                string name = endOrNewLine is null ? "Comment Line" : "Comment Block";
                HandleGroup(name, start, endOrNewLine, GroupOptions.None, commentSymbol);
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
            dfaSymbols?.Add(whitespaceHandle, WhitespaceName, TokenSymbolKind.Noise);
            regexBuilder?.Add(Regex.Accept(whitespaceRegex, whitespaceHandle, lowestPriority: true));
        }

        // Add miscellaneous noise symbols.
        foreach ((string name, Regex regex) in globalOptions.NoiseSymbols)
        {
            TokenSymbolHandle handle = writer.AddTokenSymbol(writer.GetOrAddString(name), TokenSymbolAttributes.Noise);
            dfaSymbols?.Add(handle, name, TokenSymbolKind.Noise);
            regexBuilder?.Add(Regex.Accept(regex, handle, lowestPriority: true));
        }

        // Build state machines if they are requested.
        if (dfaSymbols is not null)
        {
            Regex regex = Regex.Choice(regexBuilder!.DrainToImmutable());
            DfaBuildOptions dfaBuildOptions = DfaBuildOptions.PrioritizeSymbols;
            if (globalOptions.CaseSensitivity is not CaseSensitivity.CaseInsensitive)
            {
                dfaBuildOptions |= DfaBuildOptions.CaseSensitive;
            }
            var dfaBuild = new DfaBuild<char>(dfaSymbols.GetName, writer.TokenSymbolCount, log, options.CancellationToken);
            var dfaWriter = new DfaWriter<char>();
            if (dfaBuild.Build(regex, dfaWriter, dfaBuildOptions, options.MaxTokenizerStates))
            {
                if (groupDfaRegexes is not null)
                {
                    foreach (Regex? r in groupDfaRegexes)
                    {
                        int groupStartState = dfaWriter.StateCount;
                        if (r is null || !dfaBuild.Build(r, dfaWriter, dfaBuildOptions))
                        {
                            groupStartState = 0;
                        }
                        dfaWriter.AddGroupStartState(groupStartState);
                    }
                }
                writer.AddStateMachine(dfaWriter);
            }
        }

        if ((artifacts & BuilderArtifacts.GrammarLrStateMachine) != 0)
        {
            var conflictResolver = operatorScope is not null
                ? new OperatorScopeConflictResolver(operatorScope, operatorSymbolMap!, literalsCaseInsensitive, log)
                : null;
            var syntaxProvider = new GrammarSyntaxProvider(grammarDefinition, productionMembers);
            writer.AddStateMachine(LalrBuild.Build(syntaxProvider, conflictResolver, log, options.CancellationToken));
        }

        // Set grammar info.
        NonterminalHandle startSymbol = (NonterminalHandle)symbolMap[grammarDefinition.StartSymbol];
        writer.SetGrammarInfo(writer.GetOrAddString(grammarDefinition.GrammarName), startSymbol, grammarDefinition.Attributes);

        return Grammar.Load(writer.ToImmutableArray());

        void HandleGroup(string name, string start, string? endOrNewLine, GroupOptions options, TokenSymbolHandle container)
        {
            TokenSymbolHandle startHandle = writer.AddTokenSymbol(writer.GetOrAddString(start), TokenSymbolAttributes.GroupStart);
            dfaSymbols?.Add(startHandle, start, TokenSymbolKind.GroupStart);
            Regex startRegex = GetRegexForLiteral(start);
            regexBuilder?.Add(Regex.Accept(startRegex, startHandle, lowestPriority: false));
            Regex endRegex;
            TokenSymbolHandle endHandle;
            GroupAttributes flags = GroupAttributes.AdvanceByCharacter;
            if (endOrNewLine is null)
            {
                endRegex = NewLineRegex;
                endHandle = GetOrCreateNewLineForGroupEnd();
                flags |= GroupAttributes.EndsOnEndOfInput | GroupAttributes.KeepEndToken;
            }
            else
            {
                endRegex = GetRegexForLiteral(endOrNewLine);
                endHandle = GetOrCreateGroupEndLiteral(endOrNewLine);
            }
            bool isRecursive = (options & GroupOptions.Recursive) != 0;
            GroupHandle groupHandle = writer.AddGroup(writer.GetOrAddString(name), container, flags, startHandle, endHandle, isRecursive ? 1 : 0);
            if (isRecursive)
            {
                writer.AddGroupNesting(groupHandle);
            }
            if (regexBuilder is not null)
            {
                Regex endRegexWithAccept = Regex.Accept(endRegex, endHandle, lowestPriority: false);
                bool addEndRegexToMainDfa = true;
                groupDfaRegexes?.Add(GetGroupRegex(start, endRegexWithAccept, isRecursive, flags, out addEndRegexToMainDfa));
                if (addEndRegexToMainDfa)
                {
                    // The regex might be added multiple times, but it's OK since all accept
                    // the same symbol, and won't cause any conflicts.
                    regexBuilder.Add(endRegexWithAccept);
                }
            }
        }

        // Gets the handle to a group end literal symbol, creating it if it does not exist.
        // Multiple groups can end with the same symbol without causing a conflict, because
        // we always know how to end a group when we are inside of it.
        // In earlier versions of Farkle, group end symbols were structurally equatable by
        // their content (which BTW was wrong because it did not consider the case sensitivity
        // of each grammar) and conflicts between them were resolved automatically.
        // Because now each token symbol is identified by a number, we need to do the
        // bookkeeping ourselves. By storing the strings inside the general symbol map, we
        // also avoid conflicts between group end symbols and literals, which was not possible
        // before.
        TokenSymbolHandle GetOrCreateGroupEndLiteral(string content)
        {
            if (symbolMap.TryGetValue(content, out EntityHandle existingHandle))
            {
                return (TokenSymbolHandle)existingHandle;
            }
            TokenSymbolHandle handle = writer.AddTokenSymbol(writer.GetOrAddString(content), TokenSymbolAttributes.None);
            dfaSymbols?.Add(handle, content, TokenSymbolKind.GroupEnd);
            symbolMap.Add(content, handle);
            return handle;
        }

        Regex? GetTerminalRegex(ISymbolBase symbol)
        {
            return symbol switch
            {
                Terminal terminal => terminal.Regex,
                Literal literal => GetRegexForLiteral(literal.Value),
                NewLine => NewLineRegex,
                _ => null
            };
        }

        Regex GetRegexForLiteral(string literal)
        {
            Regex regex = Regex.Literal(literal);
            if (literalsCaseInsensitive)
            {
                regex = regex.CaseInsensitive();
            }
            return regex;
        }

        TokenSymbolHandle GetOrCreateNewLineForGroupEnd()
        {
            if (!newLineHandle.HasValue)
            {
                string name = NewLine.Instance.Name;
                newLineHandle = writer.AddTokenSymbol(writer.GetOrAddString(name),
                    autoWhitespace ? TokenSymbolAttributes.Noise : TokenSymbolAttributes.None);
                dfaSymbols?.Add(newLineHandle, name, autoWhitespace ? TokenSymbolKind.Noise : TokenSymbolKind.GroupEnd);
                regexBuilder?.Add(Regex.Accept(NewLineRegex, newLineHandle, lowestPriority: false));
            }
            return newLineHandle;
        }
    }

    private sealed class SymbolNameProvider(int sizeHint)
    {
        private readonly Dictionary<TokenSymbolHandle, (string Name, TokenSymbolKind Kind)> _symbolNames = new(sizeHint);

        private readonly Dictionary<string, int> _nameKinds = new(sizeHint);

        private bool ShouldDisambiguate(string name) =>
            _nameKinds.TryGetValue(name, out int kind) && !BitOperations.IsPow2(kind);

        public void Add(TokenSymbolHandle handle, string name, TokenSymbolKind kind)
        {
            _symbolNames.Add(handle, (name, kind));
            if (_nameKinds.TryGetValue(name, out int existingKind))
            {
                _nameKinds[name] = existingKind | (1 << (int)kind);
            }
            else
            {
                _nameKinds[name] = 1 << (int)kind;
            }
        }

        public BuilderSymbolName GetName(TokenSymbolHandle symbol)
        {
            var (name, kind) = _symbolNames[symbol];
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

            int productionMemberIndex = 0;
            for (int i = 0; i < _productionMemberBounds.Length; i++)
            {
                int memberCount = _grammarDefinition.Productions[i].Members.Length;
                _productionMemberBounds[i] = (productionMemberIndex, memberCount);
                productionMemberIndex += memberCount;
            }
        }

        public int TerminalCount => _grammarDefinition.Terminals.Count;

        public int NonterminalCount => _grammarDefinition.Nonterminals.Count;

        public int ProductionCount => _grammarDefinition.Productions.Count;

        public int StartSymbol => 0;

        public string GetTerminalName(int index) => _grammarDefinition.Terminals[index].Name;

        public string GetNonterminalName(int index) => _grammarDefinition.Nonterminals[index].Name;

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
