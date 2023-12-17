// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

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

    public abstract Regex GetRegex();

    public static RegexStringHolder Create(string pattern) => new Default(pattern);

    private sealed class Default(string pattern) : RegexStringHolder
    {
        public string Pattern { get; } = pattern;

        public override Regex GetRegex() => throw new NotImplementedException();
    }
}
