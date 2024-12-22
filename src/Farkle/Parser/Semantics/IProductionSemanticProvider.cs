// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Parser.Semantics;

/// <summary>
/// Provides an interface to run semantic actions on a reduced production.
/// </summary>
/// <seealso cref="ISemanticProvider{TChar, T}"/>
public interface IProductionSemanticProvider
{
    /// <summary>
    /// Combines the semantic values of the members of a production into a single object.
    /// This method is called by the parser when a production is reduced.
    /// </summary>
    /// <param name="parserState">The state of the parsing operation.</param>
    /// <param name="production">The identifier of the production that got reduced.</param>
    /// <param name="members">A <see cref="Span{T}"/> with the semantic values of
    /// each member of the production. The span can be used as a scratch buffer by
    /// the method and does not have to be cleared at the end.</param>
    /// <returns>The semantic value of the production as a whole.</returns>
    public object? Fuse(ref ParserState parserState, ProductionHandle production, Span<object?> members);
}
