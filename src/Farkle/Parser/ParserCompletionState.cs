// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Parser;

/// <summary>
/// Tracks whether a parsing operation has completed and its result.
/// </summary>
/// <typeparam name="T">The type of values the parsing operation returns
/// in case of success.</typeparam>
/// <remarks>
/// This is a mutable value type that must be passed around by reference.
/// </remarks>
public struct ParserCompletionState<T>
{
    private ParserResult<T> _result;

    /// <summary>
    /// Whether the parsing operation has completed.
    /// </summary>
    /// <seealso cref="SetResult"/>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// The result of the parsing operation.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="IsCompleted"/>
    /// is <see langword="false"/>.</exception>
    public readonly ParserResult<T> Result
    {
        get
        {
            if (!IsCompleted)
            {
                Fail();
            }
            return _result;

            static void Fail() => throw new InvalidOperationException(Resources.Parser_ResultNotSet);
        }
    }

    /// <summary>
    /// Completes a parsing operation.
    /// </summary>
    /// <param name="result">The value that will be assigned to
    /// <see cref="Result"/>.</param>
    /// <exception cref="InvalidOperationException">The parsing operation has
    /// already been completed by a previous invocation of <see cref="SetResult"/>
    /// or one of the methods in <see cref="ParserCompletionStateExtensions"/>.</exception>
    public void SetResult(ParserResult<T> result)
    {
        if (IsCompleted)
        {
            Fail();
        }
        IsCompleted = true;
        _result = result;

        static void Fail() => throw new InvalidOperationException(Resources.Parser_ResultAlreadySet);
    }
}

/// <summary>
/// Provides convenience extension methods for <see cref="ParserCompletionState{T}"/>.
/// </summary>
public static class ParserCompletionStateExtensions
{
    /// <summary>
    /// Successfully completes a parsing operation.
    /// </summary>
    /// <param name="state">A reference to the <see cref="ParserCompletionState{T}"/>
    /// that will hold the result.</param>
    /// <param name="value">The success value.</param>
    public static void SetSuccess<T>(this ref ParserCompletionState<T> state, T value) =>
        state.SetResult(ParserResult.CreateSuccess(value));

    /// <summary>
    /// Fails a parsing operation.
    /// </summary>
    /// <param name="state">A reference to the <see cref="ParserCompletionState{T}"/>
    /// that will hold the result.</param>
    /// <param name="error">The error value.</param>
    public static void SetError<T>(this ref ParserCompletionState<T> state, object error) =>
        state.SetResult(ParserResult.CreateError<T>(error));
}
