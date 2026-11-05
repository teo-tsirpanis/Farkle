// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Specifies when the parser may perform lookahead correction.
/// </summary>
/// <remarks>
/// <para>
/// Certain features of the builder such as state merging by LALR(1) and IELR(1),
/// or the use of non-associative operators can produce grammars that exhibit potentially
/// undesirable behavior, such as delayed detection of syntax errors or incorrect reporting
/// of expected symbols.
/// </para>
/// <para>
/// Lookahead correction is a mechanism that mitigates these issues. When it is enabled, the
/// parser will check that reducing a token will not lead to a syntax error, before actually
/// performing the reduction and executing any semantic actions.
/// </para>
/// </remarks>
/// <seealso cref="CharParser{T}.WithLookaheadCorrection(LookaheadCorrectionOption)"/>
public enum LookaheadCorrectionOption
{
    /// <summary>
    /// The parser never performs lookahead correction.
    /// </summary>
    Never,
    /// <summary>
    /// The parser performs lookahead correction only when a syntax error occurs, in order
    /// to compute a precise set of expected symbols.
    /// </summary>
    /// <remarks>
    /// This is the default value, balancing performance with diagnosability.
    /// </remarks>
    OnSyntaxErrors,
    /// <summary>
    /// The parser always performs lookahead correction, including before attempting to reduce
    /// a production. At a slight performance cost, this allows the parser to detect syntax errors
    /// earlier, before running any semantic actions.
    /// </summary>
    Always,
}
