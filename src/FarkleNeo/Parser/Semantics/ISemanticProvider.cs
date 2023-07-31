// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser.Semantics;

/// <summary>
/// Provides an interface to customize the semantic analysis stage of Farkle's parsers.
/// </summary>
/// <typeparam name="TChar">The type of characters that are parsed. Usually it is
/// <see cref="char"/> or <see cref="byte"/> (not supported by Farkle's built-in
/// parsers).</typeparam>
/// <typeparam name="T">The type of semantic values produced for the root
/// symbol of the language. This type is covariant.</typeparam>
/// <remarks>
/// <para>
/// This interface has no members. It merely inherits <see cref="ITokenSemanticProvider{TChar}"/>
/// and <see cref="IProductionSemanticProvider"/>.
/// </para>
/// <para>
/// Implementations of this interface must ensure that the semantic values of the
/// root symbol of the language are of type <typeparamref name="T"/>.
/// </para>
/// </remarks>
public interface ISemanticProvider<TChar, out T> : ITokenSemanticProvider<TChar>,
    IProductionSemanticProvider
{ }
