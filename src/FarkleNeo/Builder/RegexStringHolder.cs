// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics;

namespace Farkle.Builder;

/// <summary>
/// Lazily parses a regex string into a <see cref="Regex"/>.
/// </summary>
/// <remarks>
/// This class allows trimming the string regex parsing code
/// if string regexes are not used and no grammar gets built.
/// </remarks>
internal abstract class RegexStringHolder
{
    private RegexStringHolder() { }

    /// <summary>
    /// The string pattern of the regex.
    /// </summary>
    public abstract string Pattern { get; }

    /// <summary>
    /// Parses the regex string.
    /// </summary>
    /// <returns>A <see cref="Regex"/> if parsing was successful,
    /// or an <see cref="object"/> if not.</returns>
    public abstract object GetRegexOrError();

    public static RegexStringHolder Create(string pattern) => new Default(pattern);

    [DebuggerDisplay("{Pattern,nq}")]
    private sealed class Default(string pattern) : RegexStringHolder
    {
        private object? _result;

        public override string Pattern { get; } = pattern;

        public override object GetRegexOrError()
        {
            if (_result is {} result)
            {
                return result;
            }

            var parserResult = RegexGrammar.Parser.Parse(Pattern);
            result = parserResult.IsSuccess ? parserResult.Value : parserResult.Error;
            Interlocked.CompareExchange(ref _result, result, null);

            return _result;
        }
    }
}
