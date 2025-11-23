// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Marks an <see cref="IGrammarBuilder"/> factory method to be precompiled.
/// </summary>
/// <remarks>
/// This attribute must be applied on non-generic, static, parameterless
/// methods that return a type that implements <see cref="IGrammarBuilder"/>.
/// At build time, the precompiler will run this method, build the returned
/// grammar builder, and embed the grammar to the assembly.
/// </remarks>
/// <seealso cref="PrecompilerOutputAttribute"/>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrecompilerInputAttribute : Attribute
{
    /// <summary>
    /// A string value that disambiguates between multiple uses of
    /// <see cref="PrecompilerInputAttribute"/> on the same type.
    /// </summary>
    /// <remarks>
    /// If set, the same key must be set to the corresponding methods with
    /// <see cref="PrecompilerOutputAttribute"/>. Two attributes on the same
    /// type must not have the same key.
    /// </remarks>
    /// <seealso cref="PrecompilerOutputAttribute.Key"/>
    public string? Key { get; set; }

    // Mirroring all properties of BuilderOptions.

    /// <inheritdoc cref="BuilderOptions.MaxTokenizerStates"/>
    /// <seealso cref="BuilderOptions.MaxTokenizerStates"/>
    public int MaxTokenizerStates { get; set; } = -1;

    /// <inheritdoc cref="BuilderOptions.EmitGroupOptimizedDfa"/>
    /// <seealso cref="BuilderOptions.EmitGroupOptimizedDfa"/>
    internal bool EmitGroupOptimizedDfa { get; set; } = true;
}
