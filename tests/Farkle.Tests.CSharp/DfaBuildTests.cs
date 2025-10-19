// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;
using Farkle.Grammars;
using Farkle.Parser;
using Farkle.Parser.Semantics;
using Farkle.Parser.Tokenizers;

namespace Farkle.Tests.CSharp;

internal class DfaBuildTests
{
    [TestCase(Regex.CharsFlags.None, "fooo", true)]
    [TestCase(Regex.CharsFlags.None, "bar", true)]
    [TestCase(Regex.CharsFlags.HighPriorityInverted, "fooo", true)]
    [TestCase(Regex.CharsFlags.HighPriorityInverted, "bar", false)]
    public void TestHighPriorityInverted(Regex.CharsFlags flags, string input, bool expectedSuccess)
    {
        var regex = Regex.Chars([('b', 'b')], flags) | Regex.FromRegexString("fooo|bar");
        var tokenizer = BuildLengthReturningTokenizer(regex);

        var result = GetTokenLength(tokenizer, input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Property(nameof(ParserResult<>.IsSuccess)).EqualTo(expectedSuccess));
            if (expectedSuccess)
            {
                Assert.That(result, Has.Property(nameof(ParserResult<>.Value)).EqualTo(input.Length));
            }
        }
    }

    static Tokenizer<char> BuildLengthReturningTokenizer(Regex regex)
    {
        var parser = Terminal.Create("S", regex).AutoWhitespace(false).BuildSyntaxCheck();
        Assert.That(parser.IsFailing, Is.False);
        return parser.GetTokenizer();
    }

    static ParserResult<int> GetTokenLength(Tokenizer<char> tokenizer, string input)
    {
        ParserState state = new();
        var inputReader = new ParserInputReader<char>(ref state, input, isFinal: true);
        bool gotToken = tokenizer.TryGetNextToken(ref inputReader, LengthReturningSemanticProvider.Instance, out TokenizerResult result);
        Assert.That(gotToken);
        return result.IsSuccess ? ParserResult.CreateSuccess((int)result.Data!) : ParserResult.CreateError<int>(result.Data);
    }

    private sealed class LengthReturningSemanticProvider : ITokenSemanticProvider<char>
    {
        public static LengthReturningSemanticProvider Instance { get; } = new();

        public object? Transform(ref ParserState parserState, TokenSymbolHandle symbol, ReadOnlySpan<char> characters) =>
            characters.Length;
    }
}
