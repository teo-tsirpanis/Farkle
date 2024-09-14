// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Parser.Semantics;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Farkle.Parser.Tokenizers;

internal sealed class ChainedTokenizer<TChar> : Tokenizer<TChar>
{
    internal ImmutableArray<Tokenizer<TChar>> Components { get; }

    private ChainedTokenizer(ImmutableArray<Tokenizer<TChar>> components)
    {
        // We can skip it because we _are_ the chained tokenizer wrapping.
        CanSkipChainedTokenizerWrapping = true;
        Components = components;
    }

    internal static Tokenizer<TChar> Create(Tokenizer<TChar> tokenizer)
    {
        if (tokenizer.CanSkipChainedTokenizerWrapping)
        {
            return tokenizer;
        }
        return new ChainedTokenizer<TChar>([tokenizer]);
    }

    internal static Tokenizer<TChar> Create(ImmutableArray<Tokenizer<TChar>> components)
    {
        Debug.Assert(!components.IsDefaultOrEmpty);
        if (components is [{ CanSkipChainedTokenizerWrapping: true } tokenizer])
        {
            return tokenizer;
        }
        return new ChainedTokenizer<TChar>(components);
    }

    public override bool TryGetNextToken(ref ParserInputReader<TChar> input, ITokenSemanticProvider<TChar> semanticProvider, out TokenizerResult result)
    {
        // We mark this parser operation as supporting suspending the tokenizer.
        // It might cause some issues if the tokenizer changes in the middle of the
        // operation but this is not a supported scenario.
        input.State.Attributes =
            ParserStateAttributes.TokenizerSupportsSuspending
            | (Components.Length >= 1 ? ParserStateAttributes.HasMoreThanOneTokenizerInChain : 0);
        // Get the state of the chained tokenizer. If we have not suspended before,
        // it will be null.
        var tokenizerState = input.GetChainedTokenizerStateOrNull();
        int startIndex = 0;
        // Check if we are resuming from a previous suspension.
        if (tokenizerState is { IsSuspended: true })
        {
            // If we are, get the tokenizer to resume and clear it.
            // The latter "unsuspends" the tokenizer.
            Tokenizer<TChar> tokenizer = tokenizerState.TokenizerToResume;
            tokenizerState.TokenizerToResume = null;
            Debug.Assert(!tokenizerState.IsSuspended);
            // Invoke the resuming tokenizer.
            try
            {
                if (tokenizer.TryGetNextToken(ref input, semanticProvider, out result))
                {
                    // If the tokenizer returned with a result, return it. It might have
                    // suspended again but since we still execute the resuming tokenizer
                    // we don't have to advance NextChainIndex.
                    return true;
                }
            }
            // Catch ParserApplicationException thrown by third-party tokenizers.
            // First-party tokenizers that opt-out of the wrapping must catch it
            // themselves.
            catch (ParserApplicationException ex)
            {
                result = TokenizerResult.CreateError(ex.GetErrorObject(input.State.CurrentPosition));
                return true;
            }
            // If the tokenizer did not return a result but suspended again, we
            // return.
            if (tokenizerState.IsSuspended)
            {
                return false;
            }
            // And if the tokenizer did not suspend, we set to continue the regular
            // tokenizer chain at NextChainIndex.
            startIndex = tokenizerState.NextChainIndex;
        }

        long charactersConsumed;
        do
        {
            charactersConsumed = input.State.TotalCharactersConsumed;
            // Run all tokenizers, starting from the one we left off if we are resuming from a suspension.
            // The goal is, if we have four tokenizers and have suspended at the third, to run tokenizers 4, 1, 2 and 3.
            // We stop if a tokenizer returns true, suspends, or a full loop is made without consuming any characters.
            for (int i = 0; i < Components.Length; i++, startIndex++)
            {
                // Wrap around without using the modulo operator.
                if (startIndex == Components.Length)
                {
                    startIndex = 0;
                }

                // We invoke the next tokenizer in the chain.
                bool foundToken;
                try
                {
                    foundToken = Components[startIndex].TryGetNextToken(ref input, semanticProvider, out result);
                }
                // Catch ParserApplicationException thrown by third-party tokenizers.
                // First-party tokenizers that opt-out of the wrapping must catch it
                // themselves.
                catch (ParserApplicationException ex)
                {
                    result = TokenizerResult.CreateError(ex.GetErrorObject(input.State.CurrentPosition));
                    foundToken = true;
                }
                // Because in the main loop when we suspend we must update NextChainIndex,
                // we must always check if we have suspended after invoking a tokenizer.
                // If our tokenizer state is null, we check again in case we have suspended
                // for the first time.
                tokenizerState ??= input.GetChainedTokenizerStateOrNull();
                // If there is a state and it is suspended, we set NextChainIndex and return
                // whatever the result of the tokenizer was.
                if (tokenizerState is { IsSuspended: true })
                {
                    tokenizerState.NextChainIndex = startIndex + 1;
                    return foundToken;
                }
                // After checking for suspension, we stop the loop if we have found a token.
                // In this case, the next time the chain will start all over.
                if (foundToken)
                {
                    return true;
                }
            }
        } while (charactersConsumed != input.State.TotalCharactersConsumed);

        // If we have reached the end of the chain without finding a token, we return false.
        result = default;
        return false;
    }
}
