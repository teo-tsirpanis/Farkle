// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Diagnostics;
using Farkle.Parser;

namespace Farkle;

/// <summary>
/// Represents an application-specific error that occurred during parsing.
/// </summary>
/// <remarks>
/// Farkle will catch this exception when thrown from a semantic provider
/// or tokenizer, and gracefully fail the parsing process with a
/// user-specified error object.
/// </remarks>
public sealed class ParserApplicationException : Exception
{
    /// <summary>
    /// Whether the parser's position should be included in the error object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If set to <see langword="true"/>, Farkle will wrap <see cref="Error"/> in
    /// a <see cref="ParserDiagnostic"/> with the parser's <see cref="ParserState.CurrentPosition"/>
    /// at the time of the error, and return it to the user.
    /// </para>
    /// <para>
    /// This property is ignored if <see cref="Error"/> is already a <see cref="ParserDiagnostic"/>.
    /// </para>
    /// </remarks>
    public bool AutoSetPosition { get; }

    /// <summary>
    /// The error object that will be returned from the parser.
    /// </summary>
    public object Error { get; }

    internal object GetErrorObject(TextPosition position) => Error switch {
        ParserDiagnostic => Error,
        _ when AutoSetPosition => new ParserDiagnostic(position, Error),
        _ => Error
    };

    /// <summary>
    /// Returns the message of the <see cref="ParserApplicationException"/>.
    /// It is the string representation of the <see cref="Error"/> property.
    /// </summary>
    public override string Message => Error.ToString() ?? "";

    /// <summary>
    /// Creates a <see cref="ParserApplicationException"/>.
    /// </summary>
    /// <param name="error">The error object that will be returned to the parser.</param>
    /// <param name="autoSetPosition">Whether the parser's position should be included in
    /// the error object. Defaults to <see langword="true"/>.</param>
    public ParserApplicationException(object error, bool autoSetPosition = true)
    {
        // Farkle requires error objects to not be null, but throwing
        // another exception while creating an exception is not very helpful.
        Error = error ?? "<undefined>";
        AutoSetPosition = autoSetPosition;
    }
}
