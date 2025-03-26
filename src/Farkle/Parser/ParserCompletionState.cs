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
    /// already been set as completed.</exception>
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

    /// <summary>
    /// Successfully completes a parsing operation.
    /// </summary>
    /// <param name="value">The success value.</param>
    public void SetSuccess(T value) => SetResult(ParserResult.CreateSuccess(value));

    /// <summary>
    /// Fails a parsing operation.
    /// </summary>
    /// <param name="error">The error value.</param>
    public void SetError(object error) => SetResult(ParserResult.CreateError<T>(error));
}
