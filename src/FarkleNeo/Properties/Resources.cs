// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;
#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
using System.Text;

namespace Farkle;

[ExcludeFromCodeCoverage]
internal static class Resources
{
    private static readonly bool s_usingResourceKeys = AppContext.TryGetSwitch("System.Resources.UseSystemResourceKeys", out bool usingResourceKeys) && usingResourceKeys;

    private static ResourceManager? s_resourceManager;

    public static ResourceManager ResourceManager => s_resourceManager ??= new ResourceManager("Farkle.Resources", typeof(Resources).Assembly);

    // This method is used to decide if we need to append the exception message
    // parameters to the message when calling SR.Format. By default it returns
    // the value of System.Resources.UseSystemResourceKeys AppContext switch
    // or false if not specified.  Native code generators can replace the value
    // this returns based on user input at the time of native code generation.
    // The trimming tools are also capable of replacing the value of this method
    // when the application is being trimmed.
    internal static bool UsingResourceKeys() => s_usingResourceKeys;

    public static string GetResourceString(string resourceKey, IFormatProvider? formatProvider = null, string? defaultValue = null)
    {
        if (UsingResourceKeys())
        {
            return defaultValue ?? resourceKey;
        }

        return ResourceManager.GetString(resourceKey, formatProvider as CultureInfo)!;
    }

#if NET8_0_OR_GREATER
    // Relying on reference equality of strings is usually discouraged, but here
    // we rely on an implementation detail of ResourceManager that caches the strings
    // it returns, so that we can cache the parsed CompositeFormat instances.
    private static readonly ConditionalWeakTable<string, CompositeFormat> s_compositeFormatCache = new();

    public static CompositeFormat GetCompositeFormat(string x) =>
        s_compositeFormatCache.GetValue(x, CompositeFormat.Parse);

    public static bool TryWrite<T>(Span<char> destination, IFormatProvider? formatProvider, string resourceKey, out int charsWritten, T arg)
    {
        if (UsingResourceKeys())
        {
            return destination.TryWrite(formatProvider, $"{resourceKey}, {arg}", out charsWritten);
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return destination.TryWrite(formatProvider, GetCompositeFormat(msg), out charsWritten, arg);
    }

    public static bool TryWrite<T1, T2>(Span<char> destination, IFormatProvider? formatProvider, string resourceKey, out int charsWritten, T1 arg1, T2 arg2)
    {
        if (UsingResourceKeys())
        {
            return destination.TryWrite(formatProvider, $"{resourceKey}, {arg1}, {arg2}", out charsWritten);
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return destination.TryWrite(formatProvider, GetCompositeFormat(msg), out charsWritten, arg1, arg2);
    }

    public static bool TryWrite<T1, T2, T3>(Span<char> destination, IFormatProvider? formatProvider, string resourceKey, out int charsWritten, T1 arg1, T2 arg2, T3 arg3)
    {
        if (UsingResourceKeys())
        {
            return destination.TryWrite(formatProvider, $"{resourceKey}, {arg1}, {arg2}, {arg3}", out charsWritten);
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return destination.TryWrite(formatProvider, GetCompositeFormat(msg), out charsWritten, arg1, arg2, arg3);
    }

    public static bool TryWrite(Span<char> destination, IFormatProvider? formatProvider, string resourceKey, out int charsWritten, params object[] args)
    {
        if (UsingResourceKeys())
        {
            const string Delimiter = ", ";
            var handler = new MemoryExtensions.TryWriteInterpolatedStringHandler(Delimiter.Length * args.Length, args.Length + 1, destination, formatProvider, out bool shouldAppend);
            int i = 0;
            while (shouldAppend && i < args.Length)
            {
                shouldAppend = handler.AppendFormatted(args[i++]);
                if (shouldAppend)
                {
                    shouldAppend = handler.AppendLiteral(Delimiter);
                }
            }
            return destination.TryWrite(ref handler, out charsWritten);
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return destination.TryWrite(formatProvider, GetCompositeFormat(msg), out charsWritten, args);
    }

#else
    public static string GetCompositeFormat(string x) => x;
#endif

    public static string Format<T>(IFormatProvider? formatProvider, string resourceKey, T arg)
    {
        if (UsingResourceKeys())
        {
#if NET6_0_OR_GREATER
            return string.Create(formatProvider, $"{resourceKey}, {arg}");
#else
            return ((FormattableString)$"{resourceKey}, {arg}").ToString(formatProvider);
#endif
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return string.Format(formatProvider, GetCompositeFormat(msg), arg);
    }

    public static string Format<T1, T2>(IFormatProvider? formatProvider, string resourceKey, T1 arg1, T2 arg2)
    {
        if (UsingResourceKeys())
        {
#if NET6_0_OR_GREATER
            return string.Create(formatProvider, $"{resourceKey}, {arg1}, {arg2}");
#else
            return ((FormattableString)$"{resourceKey}, {arg1}, {arg2}").ToString(formatProvider);
#endif
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return string.Format(formatProvider, GetCompositeFormat(msg), arg1, arg2);
    }

    public static string Format<T1, T2, T3>(IFormatProvider? formatProvider, string resourceKey, T1 arg1, T2 arg2, T3 arg3)
    {
        if (UsingResourceKeys())
        {
#if NET6_0_OR_GREATER
            return string.Create(formatProvider, $"{resourceKey}, {arg1}, {arg2}, {arg3}");
#else
            return ((FormattableString)$"{resourceKey}, {arg1}, {arg2}, {arg3}").ToString(formatProvider);
#endif
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return string.Format(formatProvider, GetCompositeFormat(msg), arg1, arg2, arg3);
    }

    public static string Format(IFormatProvider? formatProvider, string resourceKey, params object[] args)
    {
        if (UsingResourceKeys())
        {
            var sb = new StringBuilder();
            sb.Append(resourceKey);
            sb.Append(", ");
            for (int i = 0; i < args.Length; i++)
            {
#if NET6_0_OR_GREATER
                sb.Append(formatProvider, $"{args[i]}");
#else
                sb.AppendFormat(formatProvider, "{0}", args[i]);
#endif
                if (i < args.Length - 1)
                {
                    sb.Append(", ");
                }
            }
            return sb.ToString();
        }

        string msg = ResourceManager.GetString(resourceKey, culture: formatProvider as CultureInfo)!;
        return string.Format(formatProvider, GetCompositeFormat(msg), args);
    }

    public static string GetEofString(IFormatProvider? formatProvider)
    {
        if (UsingResourceKeys())
        {
            return "(EOF)";
        }
        return GetResourceString(nameof(Parser_Eof), formatProvider);
    }

    public static string Grammar_TooNewFormat => GetResourceString(nameof(Grammar_TooNewFormat));

    public static string Grammar_TooOldFormat => GetResourceString(nameof(Grammar_TooOldFormat));

    public static string Grammar_EgtNeoNotSupported => GetResourceString(nameof(Grammar_EgtNeoNotSupported));

    public static string Grammar_GoldParserMustConvert => GetResourceString(nameof(Grammar_GoldParserMustConvert));

    public static string Grammar_UnrecognizedFormat => GetResourceString(nameof(Grammar_UnrecognizedFormat));

    public static string Grammar_Farkle7MustOpen => GetResourceString(nameof(Grammar_Farkle7MustOpen));

    public static string Grammar_FailedToConvert => GetResourceString(nameof(Grammar_FailedToConvert));

    public static string Parser_ResultAlreadySet => GetResourceString(nameof(Parser_ResultAlreadySet));

    public static string Parser_ResultNotSet => GetResourceString(nameof(Parser_ResultNotSet));

    public static string ChainedTokenizerBuilder_NoGrammar => GetResourceString(nameof(ChainedTokenizerBuilder_NoGrammar));

    public static string ChainedTokenizerBuilder_NoDefaultTokenizer => GetResourceString(nameof(ChainedTokenizerBuilder_NoDefaultTokenizer));

    public static string Tokenizer_AlreadySuspended => GetResourceString(nameof(Tokenizer_AlreadySuspended));

    public static string Parser_UnrecognizedToken => GetResourceString(nameof(Parser_UnrecognizedToken));

    public static string Parser_UnexpectedEndOfInputInGroup => GetResourceString(nameof(Parser_UnexpectedEndOfInputInGroup));

    public static string Parser_UnexpectedToken => GetResourceString(nameof(Parser_UnexpectedToken));

    public static string Parser_Eof => GetResourceString(nameof(Parser_Eof));

    public static string Parser_UnparsableGrammar => GetResourceString(nameof(Parser_UnparsableGrammar));

    public static string Parser_UnparsableGrammar_Critical => GetResourceString(nameof(Parser_UnparsableGrammar_Critical));

    public static string Parser_GrammarLrMissing => GetResourceString(nameof(Parser_GrammarLrMissing));

    public static string Parser_GrammarLrProblem => GetResourceString(nameof(Parser_GrammarLrProblem));

    public static string Parser_GrammarDfaMissing => GetResourceString(nameof(Parser_GrammarDfaMissing));

    public static string Parser_GrammarDfaProblem => GetResourceString(nameof(Parser_GrammarDfaProblem));

    public static string Builder_RegexCharacterRangeReverseOrder => GetResourceString(nameof(Builder_RegexCharacterRangeReverseOrder));

    public static string Builder_RegexLoopRangeReverseOrder => GetResourceString(nameof(Builder_RegexLoopRangeReverseOrder));

    public static string Builder_RegexLoopMaxTooBig => GetResourceString(nameof(Builder_RegexLoopMaxTooBig));

    public static string Builder_DfaStateLimitExceeded => GetResourceString(nameof(Builder_DfaStateLimitExceeded));

    public static string Builder_IndistinguishableSymbols => GetResourceString(nameof(Builder_IndistinguishableSymbols));

    public static string Builder_RegexContainsVoid => GetResourceString(nameof(Builder_RegexContainsVoid));

    public static string Builder_SymbolKind_Terminal => GetResourceString(nameof(Builder_SymbolKind_Terminal));

    public static string Builder_SymbolKind_Noise => GetResourceString(nameof(Builder_SymbolKind_Noise));

    public static string Builder_SymbolKind_GroupStart => GetResourceString(nameof(Builder_SymbolKind_GroupStart));

    public static string Builder_SymbolKind_GroupEnd => GetResourceString(nameof(Builder_SymbolKind_GroupEnd));

    public static string Builder_Nonterminal_EmptyProductions => GetResourceString(nameof(Builder_Nonterminal_EmptyProductions));

    public static string Builder_Nonterminal_SetProductionsManyTimes => GetResourceString(nameof(Builder_Nonterminal_SetProductionsManyTimes));

    public static string Builder_ProductionBuilderInvalidMemberObject => GetResourceString(nameof(Builder_ProductionBuilderInvalidMemberObject));

    public static string Builder_ConflictMustBeShiftReduce => GetResourceString(nameof(Builder_ConflictMustBeShiftReduce));

    public static string Builder_ConflictMustBeReduceReduce => GetResourceString(nameof(Builder_ConflictMustBeReduceReduce));

    public static string Builder_ShiftReduceConflict => GetResourceString(nameof(Builder_ShiftReduceConflict));

    public static string Builder_ReduceReduceConflict => GetResourceString(nameof(Builder_ReduceReduceConflict));

    public static string Builder_AcceptReduceConflict => GetResourceString(nameof(Builder_AcceptReduceConflict));

    public static string Builder_ConflictDescription => GetResourceString(nameof(Builder_ConflictDescription));

    public static string Builder_DuplicateSpecialName => GetResourceString(nameof(Builder_DuplicateSpecialName));

    public static string Builder_NonterminalProductionsNotSet => GetResourceString(nameof(Builder_NonterminalProductionsNotSet));

    public static string Builder_DuplicateOperatorSymbol => GetResourceString(nameof(Builder_DuplicateOperatorSymbol));

    public static string Builder_SymbolMultipleRenames => GetResourceString(nameof(Builder_SymbolMultipleRenames));

    public static string Builder_BuildingStarted => GetResourceString(nameof(Builder_BuildingStarted));

    public static string Builder_BuildingFinished => GetResourceString(nameof(Builder_BuildingFinished));

    public static string Builder_RegexStringPredefinedSetsNotSupported => GetResourceString(nameof(Builder_RegexStringPredefinedSetsNotSupported));

    public static string Builder_RegexStringUnicodeCategoriesNotSupported => GetResourceString(nameof(Builder_RegexStringUnicodeCategoriesNotSupported));

    public static string Builder_RegexStringParseError => GetResourceString(nameof(Builder_RegexStringParseError));

    public static string Warning => GetResourceString(nameof(Warning));

    public static string Error => GetResourceString(nameof(Error));
}
