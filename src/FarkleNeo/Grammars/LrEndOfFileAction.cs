// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Specifies what an LR parser will do if it encounters the end of input.
/// </summary>
public readonly struct LrEndOfFileAction : IEquatable<LrEndOfFileAction>
{
    private readonly int _value;

    private LrEndOfFileAction(int value) => _value = value;

    /// <summary>
    /// An <see cref="LrEndOfFileAction"/> that will cause a syntax error.
    /// </summary>
    /// <seealso cref="IsError"/>
    public static LrEndOfFileAction Error => default;

    /// <summary>
    /// An <see cref="LrEndOfFileAction"/> that will cause the input to be accepted.
    /// </summary>
    /// <seealso cref="IsAccept"/>
    public static LrEndOfFileAction Accept => new(1);

    /// <summary>
    /// Creates an <see cref="LrEndOfFileAction"/> that will reduce the specified <see cref="ProductionHandle"/>.
    /// </summary>
    /// <param name="production">The production to reuse.</param>
    /// <exception cref="ArgumentNullException"><paramref name="production"/>'s
    /// <see cref="ProductionHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <seealso cref="IsReduce"/>
    public static LrEndOfFileAction CreateReduce(ProductionHandle production)
    {
        if (!production.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(production));
        }
        return new(-(int)production.TableIndex);
    }

    /// <summary>
    /// Whether this <see cref="LrEndOfFileAction"/> will cause a syntax error.
    /// </summary>
    /// <seealso cref="Error"/>
    public bool IsError => _value == 0;

    /// <summary>
    /// Whether this <see cref="LrEndOfFileAction"/> will cause the input to be accepted.
    /// </summary>
    /// <seealso cref="Accept"/>
    public bool IsAccept => _value == 1;

    /// <summary>
    /// Whether this <see cref="LrEndOfFileAction"/> will reduce a production.
    /// </summary>
    /// <seealso cref="CreateReduce"/>
    public bool IsReduce => _value > 1;

    /// <summary>
    /// The production this <see cref="LrEndOfFileAction"/> will reduce.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="IsReduce"/>
    /// property is <see langword="false"/>.</exception>
    public ProductionHandle ReduceProduction
    {
        get
        {
            if (!IsReduce)
            {
                ThrowHelpers.ThrowInvalidOperationException("This action is not a reduction.");
            }
            return new((uint)-_value);
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? other) => other is LrEndOfFileAction x && Equals(x);

    /// <inheritdoc/>
    public bool Equals(LrEndOfFileAction other) => _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value;

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsReduce)
        {
            return $"Reduce production {ReduceProduction.TableIndex}";
        }

        if (IsAccept)
        {
            return "Accept";
        }

        return "Error";
    }

    /// <summary>
    /// Checks two <see cref="LrEndOfFileAction"/>s for equality.
    /// </summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    public static bool operator ==(LrEndOfFileAction left, LrEndOfFileAction right) => left.Equals(right);

    /// <summary>
    /// Checks two <see cref="LrEndOfFileAction"/>s for inequality.
    /// </summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    public static bool operator !=(LrEndOfFileAction left, LrEndOfFileAction right) => !(left==right);
}
