// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Tests.CSharp
{
    internal class TokenizerTests
    {
        /// <summary>
        /// Tests that some strings produce JSON tokens.
        /// </summary>
        [TestCase("137", "Number")]
        [TestCase("\"Hello\"", "String")]
        [TestCase(@"""\""\r\n\t \u00B8""", "String")]
        [TestCase("true", "true")]
        [TestCase("false", "false")]
        [TestCase("null", "null")]
        public void TestJsonSuccess(string text, string tokenName)
        {
            var grammar = TestUtilities.LoadGrammarFromResource("JSON.grammar.dat");
            var tokenizer = Tokenizer.Create<char>(grammar);
            Assert.Multiple(() =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var stateBox = new ParserStateBox();
#pragma warning restore CS0618 // Type or member is obsolete
                ParserInputReader<char> reader = new(stateBox, text.AsSpan());

                Assert.That(tokenizer.TryGetNextToken(ref reader, SyntaxChecker<char, object?>.Instance, out var token));
                Assert.That(token.IsSuccess);
                Assert.That(token.Position, Is.EqualTo(TextPosition.Initial));
                Assert.That(grammar.GetString(grammar.GetTokenSymbol(token.Symbol).Name), Is.EqualTo(tokenName));
            });
        }

        /// <summary>
        /// Tests that some strings either produce or not produce a JSON token,
        /// given that there might be more characters coming after the string.
        /// </summary>
        [TestCase("137", false)]
        [TestCase("\"Hello\"", true)]
        [TestCase("\"Hello", false)]
        [TestCase("true", true)]
        [TestCase("false", true)]
        [TestCase("null", true)]
        [TestCase("nul", false)]
        public void TestJsonSuccessNonFinal(string text, bool shouldFindToken)
        {
            var grammar = TestUtilities.LoadGrammarFromResource("JSON.grammar.dat");
            var tokenizer = Tokenizer.Create<char>(grammar);
#pragma warning disable CS0618 // Type or member is obsolete
            var stateBox = new ParserStateBox();
#pragma warning restore CS0618 // Type or member is obsolete
            ParserInputReader<char> reader = new(stateBox, text.AsSpan(), isFinal: false);
            Assert.That(tokenizer.TryGetNextToken(ref reader, SyntaxChecker<char, object?>.Instance, out _), Is.EqualTo(shouldFindToken));
        }

        /// <summary>
        /// Tests that some strings do not produce JSON tokens.
        /// </summary>
        [TestCase(@"""Hello")]
        [TestCase(@"""Hello\""")]
        [TestCase("tru")]
        [TestCase("fals")]
        [TestCase("nul")]
        public void TestJsonFailure(string text)
        {
            var grammar = TestUtilities.LoadGrammarFromResource("JSON.grammar.dat");
            var tokenizer = Tokenizer.Create<char>(grammar);
            Assert.Multiple(() =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var stateBox = new ParserStateBox();
#pragma warning restore CS0618 // Type or member is obsolete
                ParserInputReader<char> reader = new(stateBox, text.AsSpan());
                Assert.That(tokenizer.TryGetNextToken(ref reader, SyntaxChecker<char, object?>.Instance, out var token));
                Assert.That(token.IsSuccess, Is.False);
            });
        }

        private sealed class SyntaxChecker<TChar> : ITokenSemanticProvider<TChar>
        {
            public static readonly SyntaxChecker<TChar> Instance = new();

            public object? Transform(ref ParserState parserState, TokenSymbolHandle symbol, ReadOnlySpan<TChar> characters) => null;
        }
    }
}
