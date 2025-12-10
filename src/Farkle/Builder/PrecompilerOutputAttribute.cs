// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;

namespace Farkle.Builder;

/// <summary>
/// Enables accessing the outputs of a precompiled grammar defined with
/// <see cref="PrecompilerInputAttribute"/>.
/// Methods with this attribute applied are called <em>precompiler output methods</em>,
/// or just <em>output methods</em>.
/// </summary>
/// <remarks>
/// <para>
/// This attribute must be applied on non-generic, static, parameterless
/// methods that return a <see cref="Grammar"/> or a <see cref="CharParser{T}"/>.
/// At build time, the precompiler will update the method's body to retrieve
/// the precompiled grammar and construct the requested object.
/// </para>
/// <para>
/// The following return types are supported:
/// </para>
/// <list type="bullet">
/// <item><see cref="Grammar"/></item>
/// <item><see cref="CharParser{T}"/>, if the corresponding method with <see cref="PrecompilerInputAttribute"/>
/// (the <em>precompiler input method</em>) returns <see cref="IGrammarBuilder{T}"/>, and the grammar builder's
/// return type can be assigned to the parser's return type.</item>
/// <item><see cref="CharParser{T}"/>, if the precompiler input method returns <see cref="IGrammarBuilder"/>,
/// or the <see cref="SyntaxCheck"/> property is set to true. In this case, the parser's return type must be
/// a reference type.</item>
/// </list>
/// </remarks>
/// <seealso cref="PrecompilerInputAttribute"/>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrecompilerOutputAttribute : Attribute
{
    /// <summary>
    /// A string value that is used for disambiguation if multiple methods
    /// with <see cref="PrecompilerInputAttribute"/> exist on the same type.
    /// </summary>
    /// <seealso cref="PrecompilerInputAttribute.Key"/>
    public string? Key { get; set; }

    /// <summary>
    /// Whether to emit a syntax-checking parser.
    /// </summary>
    /// <remarks>
    /// Setting this property is required when the precompiler input method's type
    /// implements <see cref="IGrammarBuilder{T}"/>. Otherwise, a syntax-checking
    /// parser is always emitted.
    /// </remarks>
    public bool SyntaxCheck { get; set; }
}
