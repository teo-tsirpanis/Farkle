// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Builder;

/// <summary>
/// Provides options to configure the process of building a grammar.
/// </summary>
/// <remarks>
/// The options in this class do not affect the result of the build process.
/// </remarks>
public sealed class BuilderOptions
{
    /// <summary>
    /// Used to cancel the build process.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// The maximum number of states that the tokenizer can have.
    /// </summary>
    /// <remarks>
    /// This value can be used to prevent exponential blowup of the tokenizer
    /// states for certain regexes like <c>[ab]*[ab]{32}</c>. If it is zero
    /// or negative, the limit is set to an implementation-defined number
    /// that is proportional to the complexity of the input regexes.
    /// </remarks>
    public int MaxTokenizerStates { get; set; } = -1;

    internal static int GetMaxTokenizerStates(int maxTokenizerStates, int numLeaves)
    {
        if (maxTokenizerStates > 0)
        {
            return maxTokenizerStates;
        }

        long limit = (long)numLeaves * 16;
        if (limit > int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Min(256, (int)limit);
    }
}
