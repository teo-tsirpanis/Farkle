// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Grammars;

/// <summary>
/// Specifies what an LR parser will do if it encounters a terminal.
/// </summary>
public readonly struct LrTerminalAction : IEquatable<LrTerminalAction>
{
    private readonly int _value;

    private LrTerminalAction(int value) => _value = value;

    /// <summary>
    /// An <see cref="LrTerminalAction"/> that will cause a syntax error.
    /// </summary>
    /// <seealso cref="IsError"/>
    public static LrTerminalAction Error => default;

    /// <summary>
    /// Creates an <see cref="LrTerminalAction"/> that will shift to the specified state.
    /// </summary>
    /// <param name="state">The state's number, starting from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> is negative.</exception>
    /// <seealso cref="IsReduce"/>
    public static LrTerminalAction CreateShift(int state)
    {
        if (state < 0)
        {
            ThrowHelpers.ThrowArgumentOutOfRangeException(nameof(state), "State cannot be negative.");
        }
        return new(state + 1);
    }

    /// <summary>
    /// Creates an <see cref="LrTerminalAction"/> that will reduce the specified <see cref="ProductionHandle"/>.
    /// </summary>
    /// <param name="production">The production to reuse.</param>
    /// <exception cref="ArgumentNullException"><paramref name="production"/>'s
    /// <see cref="ProductionHandle.HasValue"/> property is <see langword="false"/>.</exception>
    /// <seealso cref="IsReduce"/>
    public static LrTerminalAction CreateReduce(ProductionHandle production)
    {
        if (!production.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(production));
        }
        return new(-(int)production.TableIndex);
    }

    /// <summary>
    /// Whether this <see cref="LrTerminalAction"/> will cause a syntax error.
    /// </summary>
    /// <seealso cref="Error"/>
    public bool IsError => _value == 0;

    /// <summary>
    /// Whether this <see cref="LrTerminalAction"/> will shift to another state.
    /// </summary>
    /// <seealso cref="CreateShift"/>
    public bool IsShift => _value > 0;

    /// <summary>
    /// Whether this <see cref="LrTerminalAction"/> will reduce a production.
    /// </summary>
    /// <seealso cref="CreateReduce"/>
    public bool IsReduce => _value < 0;

    /// <summary>
    /// The state this <see cref="LrTerminalAction"/> will shift to.
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
            return _value - 1;
        }
    }

    /// <summary>
    /// The production this <see cref="LrTerminalAction"/> will reduce.
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
    public override bool Equals(object? other) => other is LrTerminalAction x && Equals(x);

    /// <inheritdoc/>
    public bool Equals(LrTerminalAction other) => _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value;

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

    /// <summary>
    /// Checks two <see cref="LrTerminalAction"/>s for equality.
    /// </summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    public static bool operator ==(LrTerminalAction left, LrTerminalAction right) => left.Equals(right);

    /// <summary>
    /// Checks two <see cref="LrTerminalAction"/>s for inequality.
    /// </summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    public static bool operator !=(LrTerminalAction left, LrTerminalAction right) => !(left==right);
}
