// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Farkle.Builder.Lr;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
#pragma warning disable CS9113 // Parameter is unread.
internal readonly struct Production(int index, AugmentedSyntaxProvider syntax) : IEquatable<Production>
#pragma warning restore CS9113 // Parameter is unread.
{
    public int Index { get; } = index;

#if DEBUG
    private readonly AugmentedSyntaxProvider _debugOnlySyntax = syntax;

    [ExcludeFromCodeCoverage]
    public readonly string GetDebuggerDisplay(int dotPosition = -1)
    {
        var sb = new StringBuilder();
        sb.Append($"<{_debugOnlySyntax.GetNonterminalName(_debugOnlySyntax.GetProductionHead(Index).Index)}> →");
        var members = _debugOnlySyntax.GetProductionMembers(this);
        for (int i = 0; i < members.Count; i++)
        {
            if (i == dotPosition)
            {
                sb.Append(" •");
            }
            sb.Append(' ');
            var member = members[i];
            if (member.IsTerminal)
            {
                string name = _debugOnlySyntax.GetTerminalName(member.Index);
                if (member.Index != AugmentedSyntaxProvider.EndSymbolIndex)
                {
                    name = TokenSymbolDefinition.FormatName(name);
                }
                sb.Append(name);
            }
            else
            {
                sb.Append($"<{_debugOnlySyntax.GetNonterminalName(member.Index)}>");
            }
        }
        if (dotPosition == members.Count)
        {
            sb.Append(" •");
        }
        return sb.ToString();
    }

    private readonly string DebuggerDisplay => GetDebuggerDisplay();
#else
    private readonly string DebuggerDisplay => "Production " + Index;
#endif

    public bool Equals(Production other) => Index == other.Index;

    public override bool Equals(object? obj) => obj is Production x && Equals(x);

    public override int GetHashCode() => Index.GetHashCode();
}
