// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Provides a way to protect building of grammars from potential behavior breaking changes in future versions.
/// </summary>
/// <remarks>
/// A new feature or behavior will be introduced under a new compatibility level, if it would potentially:
/// <list type="bullet">
/// <item>Make grammars that successfully build before not build anymore.</item>
/// <item>Change the text that successfully built grammars can parse.</item>
/// </list>
/// <para>
/// Currently, setting a compatibility level is supported on individual string regexes and on the whole grammar.
/// When a new compatibility level is introduced, the previous ones will be marked as obsolete and should only
/// be used as a temporary measure.
/// </para>
/// <para>
/// When a compatibility level is not set, the latest one will be used.
/// </para>
/// </remarks>
/// <seealso cref="GrammarBuilderConfigurationExtensions.WithCompatibilityLevel"/>
/// <seealso cref="Regex.FromRegexString(string, CompatibilityLevel)"/>
public sealed class CompatibilityLevel
{
    /// <summary>
    /// The compatibility level for Farkle 7.0.
    /// </summary>
    /// <remarks>
    /// The following changes were introduced since Farkle 6.x:
    /// <list type="bullet">
    /// <item>The default value of <see cref="GrammarBuilderConfigurationExtensions.CaseSensitive(IGrammarBuilder,bool)"/>
    /// was changed from <see langword="false"/> to <see langword="true"/>.</item>
    /// <item>The default value of <see cref="GrammarBuilderConfigurationExtensions.NewLineIsNoisy"/>
    /// was changed from <see langword="false"/> to the value of
    /// <see cref="GrammarBuilderConfigurationExtensions.AutoWhitespace"/>.</item>
    /// <item>The language for string regexes is different and more in line with standard regex dialects.</item>
    /// <item><see cref="Regex.Any"/> and the <c>.</c> character in regexes match any character and no longer
    /// have lower precedence when being matched.</item>
    /// </list>
    /// </remarks>
    public static CompatibilityLevel Farkle7_0 { get; } = new();

    private CompatibilityLevel() { }
}
