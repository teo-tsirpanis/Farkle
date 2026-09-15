// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

internal class OperatorInfoProviderTests
{
    private static readonly IGrammarSymbol _terminal1 = Terminal.Virtual(nameof(_terminal1));

    private static readonly TokenSymbolHandle _terminal1Handle = new(1);

    private static readonly IGrammarSymbol _terminal2 = Terminal.Virtual(nameof(_terminal2));

    private static readonly TokenSymbolHandle _terminal2Handle = new(2);

    private static readonly object _production1Token = new();

    private static readonly IProduction _production1 = _terminal1.Appended().Append(_terminal2).WithPrecedence(_production1Token);

    private static readonly ProductionHandle _production1Handle = new(1);

    private static readonly object _production2Token = new();

    private static readonly IProduction _production2 = _terminal1.Appended().WithPrecedence(_production2Token);

    private static readonly ProductionHandle _production2Handle = new(2);

    private static readonly Dictionary<EntityHandle, object> _objectMap = new()
    {
        [_terminal1Handle] = _terminal1,
        [_terminal2Handle] = _terminal2,
        [_production1Handle] = _production1,
        [_production2Handle] = _production2
    };

    [Test]
    public void TestPrecedenceFromToken([Values] bool productionFirst)
    {
        OperatorScope operators = productionFirst
            ? [new LeftAssociative(_production1Token), new LeftAssociative(_terminal1)]
            : [new LeftAssociative(_terminal1), new LeftAssociative(_production1Token)];
        var infoProvider = new OperatorInfoProvider(operators, _objectMap, EqualityComparer<object>.Default);

        int productionPrecedence = productionFirst ? 0 : 1;
        int terminalPrecedence = 1 - productionPrecedence;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(infoProvider.GetPrecedence(_terminal1Handle), Is.EqualTo(terminalPrecedence));
            Assert.That(infoProvider.GetPrecedence(_terminal2Handle), Is.EqualTo(-1));
            Assert.That(infoProvider.GetPrecedence(_production1Handle), Is.EqualTo(productionPrecedence));
            Assert.That(infoProvider.GetPrecedence(_production2Handle), Is.EqualTo(-1));
        }
    }

    [Test]
    public void TestPrecedenceFromProductionMember([Values] bool terminal1Last)
    {
        var production = terminal1Last ? new ProductionBuilder(_terminal2, _terminal1) : new ProductionBuilder(_terminal1, _terminal2);
        Dictionary<EntityHandle, object> objectMap = new()
        {
            [_terminal1Handle] = _terminal1,
            [_terminal2Handle] = _terminal2,
            [_production1Handle] = production,
        };
        OperatorScope operators = [new LeftAssociative(_terminal1), new LeftAssociative(_terminal2)];
        var infoProvider = new OperatorInfoProvider(operators, objectMap, EqualityComparer<object>.Default);

        int terminalPrecedence = terminal1Last ? 0 : 1;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(infoProvider.GetPrecedence(_production1Handle), Is.EqualTo(terminalPrecedence));
        }
    }

    [Test]
    public void TestLiterals([Values] bool caseSensitive)
    {
        const string literal1 = nameof(literal1);
        const string literal2 = nameof(literal2);
        OperatorScope operators = [new PrecedenceOnly(literal1), new PrecedenceOnly(Terminal.Literal(literal2))];
        var objectMap = new Dictionary<EntityHandle, object>
        {
            [_terminal1Handle] = Terminal.Literal(caseSensitive ? literal1 : literal1.ToUpperInvariant()),
            [_production1Handle] = (caseSensitive ? literal2 : literal2.ToUpperInvariant()).Appended()
        };
        var infoProvider = new OperatorInfoProvider(operators, objectMap, Utilities.GetFallbackStringComparer(caseSensitive));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(infoProvider.GetPrecedence(_terminal1Handle), Is.Zero);
            Assert.That(infoProvider.GetPrecedence(_production1Handle), Is.EqualTo(1));
        }
    }
}
