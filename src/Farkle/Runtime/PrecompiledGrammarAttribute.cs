// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using Farkle.Builder;

namespace Farkle.Runtime;

/// <summary>
/// Marks the existence of a precompiled grammar embedded in an assembly.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is applied by the precompiler to RVA fields that contain precompiled
/// grammar files, allowing them to be identified and retrieved by metadata reader tools
/// without loading the assembly. The field is always defined on the same type where
/// the grammar was defined.
/// </para>
/// <para>
/// User code must not manually apply this attribute.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Field)]
public sealed class PrecompiledGrammarAttribute : Attribute
{
    /// <summary>
    /// The grammar's disambiguation key. Can be used to cross-reference the grammar file
    /// with its corresponding input and output methods.
    /// </summary>
    /// <seealso cref="PrecompilerInputAttribute.Key"/>
    /// <seealso cref="PrecompilerOutputAttribute.Key"/>
    public string? Key { get; set; }
}
