// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Farkle.Grammars;
using Farkle.Grammars.StateMachines;

namespace Farkle.Diagnostics.Builder;

/// <summary>
/// Represents a conflict in an LR parser state machine.
/// A conflict manifests when the parser can take multiple
/// possible actions when encountering a terminal.
/// </summary>
// Unlike earlier versions of Farkle, we will not include the
// reason for the conflict. This can be retrieved by logging.
public sealed class LrConflict : IFormattable
#if NET8_0_OR_GREATER
    , ISpanFormattable
#endif
{
    private readonly int _shiftState;

    private readonly ProductionHandle _reduceProduction2;

    private object GetConflictDescription()
    {
        Production reduceProduction = Grammar.GetProduction(ReduceProduction);
        switch (Kind)
        {
            case LrConflictKind.ShiftReduce:
                return LocalizedDiagnostic.Create(nameof(Resources.Builder_ShiftReduceConflict), ShiftState, reduceProduction);
            case LrConflictKind.ReduceReduce:
                Production reduceProduction2 = Grammar.GetProduction(ReduceProduction2);
                return LocalizedDiagnostic.Create(nameof(Resources.Builder_ReduceReduceConflict), reduceProduction, reduceProduction2);
            default:
                Debug.Assert(Kind == LrConflictKind.AcceptReduce);
                return LocalizedDiagnostic.Create(nameof(Resources.Builder_AcceptReduceConflict), reduceProduction);
        }
    }

    private string GetTerminalNameLocalized(IFormatProvider? provider) => TerminalOrEndOfInput.HasValue
        ? Grammar.GetTokenSymbol(TerminalOrEndOfInput).ToString()
        : Resources.GetEofString(provider);

    private LrConflict() { }

    /// <summary>
    /// The grammar whose LR state machine has this conflict.
    /// </summary>
    public required Grammar Grammar { get; init; }

    /// <summary>
    /// The index of the state with this conflict.
    /// </summary>
    public required int StateIndex { get; init; }

    /// <summary>
    /// The handle to the terminal upon whose encounter the conflict manifests.
    /// </summary>
    /// <remarks>
    /// This property can have no value (as determined by <see cref="TokenSymbolHandle.HasValue"/>)
    /// if the conflict manifests when the parser reaches the end of the input.
    /// This can never be the case in <see cref="LrConflictKind.ShiftReduce"/>
    /// conflicts, and is always the case in <see cref="LrConflictKind.AcceptReduce"/>
    /// conflicts.
    /// </remarks>
    public required TokenSymbolHandle TerminalOrEndOfInput { get; init; }

    /// <summary>
    /// The kind of the conflict.
    /// </summary>
    public required LrConflictKind Kind { get; init; }

    /// <summary>
    /// The state to shift to as one of the possible outcomes of a
    /// Shift-Reduce conflict.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not
    /// <see cref="LrConflictKind.ShiftReduce"/>.</exception>
    public int ShiftState
    {
        get
        {
            if (Kind != LrConflictKind.ShiftReduce)
            {
                ThrowHelpers.ThrowInvalidOperationExceptionLocalized(nameof(Resources.Builder_ConflictMustBeShiftReduce));
            }
            return _shiftState;
        }
        init => _shiftState = value;
    }

    /// <summary>
    /// The handle to the production to reduce, as one of the possible outcomes of the conflict.
    /// </summary>
    public required ProductionHandle ReduceProduction { get; init; }

    /// <summary>
    /// The handle to the alternative production to reduce, as one of the possible
    /// outcomes of a Reduce-Reduce conflict.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Kind"/> is not
    /// <see cref="LrConflictKind.ReduceReduce"/>.</exception>
    public ProductionHandle ReduceProduction2
    {
        get
        {
            if (!_reduceProduction2.HasValue)
            {
                ThrowHelpers.ThrowInvalidOperationExceptionLocalized(nameof(Resources.Builder_ConflictMustBeReduceReduce));
            }
            return _reduceProduction2;
        }
        init => _reduceProduction2 = value;
    }

    /// <summary>
    /// Creates an <see cref="LrConflict"/> representing a Shift-Reduce conflict.
    /// </summary>
    /// <param name="grammar">The <see cref="Grammar"/> that the conflict is in.</param>
    /// <param name="stateIndex">The index of the state with the conflict.</param>
    /// <param name="terminal">The name of the terminal upon whose encounter the conflict manifests.</param>
    /// <param name="shiftState">The index of the state to shift to.</param>
    /// <param name="reduceProduction">The production to reduce.</param>
    public static LrConflict CreateShiftReduce(Grammar grammar, int stateIndex, TokenSymbolHandle terminal,
        int shiftState, ProductionHandle reduceProduction)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(grammar);
        if (!terminal.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(terminal));
        }
        if (!reduceProduction.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(reduceProduction));
        }
        return new LrConflict
        {
            Grammar = grammar,
            StateIndex = stateIndex,
            TerminalOrEndOfInput = terminal,
            Kind = LrConflictKind.ShiftReduce,
            ShiftState = shiftState,
            ReduceProduction = reduceProduction
        };
    }

    /// <summary>
    /// Creates an <see cref="LrConflict"/> representing a Reduce-Reduce conflict.
    /// </summary>
    /// <param name="grammar">The <see cref="Grammar"/> that the conflict is in.</param>
    /// <param name="stateIndex">The index of the state with the conflict.</param>
    /// <param name="terminalOrEndOfInput">The name of the terminal upon whose encounter the conflict
    /// manifests, or <see langword="default"/> if the ocnflict manifests when encountering the end
    /// of input.</param>
    /// <param name="reduceProduction">The first production to reduce.</param>
    /// <param name="reduceProduction2">The second production to reduce.</param>
    public static LrConflict CreateReduceReduce(Grammar grammar, int stateIndex, TokenSymbolHandle terminalOrEndOfInput,
        ProductionHandle reduceProduction, ProductionHandle reduceProduction2)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(grammar);
        if (!reduceProduction.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(reduceProduction));
        }
        if (!reduceProduction2.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(reduceProduction2));
        }
        return new LrConflict
        {
            Grammar = grammar,
            StateIndex = stateIndex,
            TerminalOrEndOfInput = terminalOrEndOfInput,
            Kind = LrConflictKind.ReduceReduce,
            ReduceProduction = reduceProduction,
            ReduceProduction2 = reduceProduction2
        };
    }

    /// <summary>
    /// Creates an <see cref="LrConflict"/> representing an Accept-Reduce conflict.
    /// </summary>
    /// <param name="grammar">The <see cref="Grammar"/> that the conflict is in.</param>
    /// <param name="stateIndex">The index of the state with the conflict.</param>
    /// <param name="reduceProduction">The production to reduce.</param>
    public static LrConflict CreateAcceptReduce(Grammar grammar, int stateIndex, ProductionHandle reduceProduction)
    {
        ArgumentNullExceptionCompat.ThrowIfNull(grammar);
        if (!reduceProduction.HasValue)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(reduceProduction));
        }
        return new LrConflict
        {
            Grammar = grammar,
            StateIndex = stateIndex,
            TerminalOrEndOfInput = default,
            Kind = LrConflictKind.AcceptReduce,
            ReduceProduction = reduceProduction
        };
    }

#if NET8_0_OR_GREATER
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => Resources.TryWrite(destination, provider, nameof(Resources.Builder_ConflictDescription), out charsWritten,
            GetConflictDescription(), GetTerminalNameLocalized(provider), StateIndex);
#endif

    private string ToString(IFormatProvider? formatProvider) =>
        Resources.Format(formatProvider, nameof(Resources.Builder_ConflictDescription),
            GetConflictDescription(), GetTerminalNameLocalized(formatProvider), StateIndex);

    /// <summary>
    /// Formats the <see cref="LrConflict"/> to a string, while also supporting localization.
    /// </summary>
    /// <param name="format">Unused.</param>
    /// <param name="formatProvider">Used to select the culture to localize the conflict.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString(formatProvider);

    /// <summary>
    /// Formats the <see cref="LrConflict"/> to a string.
    /// </summary>
    public override string ToString() => ToString(null);
}
