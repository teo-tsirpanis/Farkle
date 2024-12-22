// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars.StateMachines;

/// <summary>
/// Specifies what an LR parser will do if it encounters a terminal.
/// </summary>
public readonly struct LrAction : IEquatable<LrAction>
{
    internal int Value { get; }

    internal LrAction(int value) => Value = value;

    /// <summary>
    /// An <see cref="LrAction"/> that will cause a syntax error.
    /// </summary>
    /// <seealso cref="IsError"/>
    public static LrAction Error => default;

    /// <summary>
    /// Creates an <see cref="LrAction"/> that will shift to the specified state.
    /// </summary>
    /// <param name="state">The state's number, starting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> is negative.</exception>
    /// <seealso cref="IsReduce"/>
    public static LrAction CreateShift(int state)
    {
        if (state < 0)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(state), "State cannot be negative.");
        }
        return new(state + 1);
    }

    /// <summary>
    /// Creates an <see cref="LrAction"/> that will reduce the specified <see cref="ProductionHandle"/>.
    /// </summary>
    /// <param name="production">The production to reuse.</param>
    /// <exception cref="ArgumentNullException"><paramref name="production"/>'s
    /// <see cref="ProductionHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <seealso cref="IsReduce"/>
    public static LrAction CreateReduce(ProductionHandle production)
    {
        if (!production.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(production));
        }
        return new(-(int)production.TableIndex);
    }

    /// <summary>
    /// Whether this <see cref="LrAction"/> will cause a syntax error.
    /// </summary>
    /// <seealso cref="Error"/>
    public bool IsError => Value == 0;

    /// <summary>
    /// Whether this <see cref="LrAction"/> will shift to another state.
    /// </summary>
    /// <seealso cref="CreateShift"/>
    public bool IsShift => Value > 0;

    /// <summary>
    /// Whether this <see cref="LrAction"/> will reduce a production.
    /// </summary>
    /// <seealso cref="CreateReduce"/>
    public bool IsReduce => Value < 0;

    /// <summary>
    /// The state this <see cref="LrAction"/> will shift to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="IsShift"/>
    /// property is <see langword="false"/>.</exception>
    public int ShiftState
    {
        get
        {
            if (!IsShift)
            {
                ThrowHelpers.ThrowInvalidOperationException("This action is not a shift.");
            }
            return Value - 1;
        }
    }

    /// <summary>
    /// The production this <see cref="LrAction"/> will reduce.
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
            return new((uint)-Value);
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? other) => other is LrAction x && Equals(x);

    /// <inheritdoc/>
    public bool Equals(LrAction other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsShift)
        {
            return $"Shift to state {ShiftState}";
        }

        if (IsReduce)
        {
            return $"Reduce production {ReduceProduction.TableIndex}";
        }

        return "Error";
    }

    internal string ToString(Grammar grammar)
    {
        if (IsShift)
        {
            return $"Shift to state {ShiftState}";
        }

        if (IsReduce)
        {
            return $"Reduce production {grammar.GetProduction(ReduceProduction)}";
        }

        return "Error";
    }

    /// <summary>
    /// Checks two <see cref="LrAction"/>s for equality.
    /// </summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    public static bool operator ==(LrAction left, LrAction right) => left.Equals(right);

    /// <summary>
    /// Checks two <see cref="LrAction"/>s for inequality.
    /// </summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    public static bool operator !=(LrAction left, LrAction right) => !(left==right);
}
