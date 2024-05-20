// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using Farkle.Builder;
using Farkle.Builder.Lr;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Grammars;

namespace Farkle.Tests.CSharp;

internal class OperatorScopeConflictResolverTests
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
    [TestCase(true, LrConflictResolverDecision.ChooseOption1)]
    [TestCase(false, LrConflictResolverDecision.ChooseOption2)]
    public void TestDifferentPrecedence(bool reduceFirst, LrConflictResolverDecision expectedDecision)
    {
        OperatorScope operators = reduceFirst
            ? new(new LeftAssociative(_production1Token), new LeftAssociative(_terminal1))
            : new(new LeftAssociative(_terminal1), new LeftAssociative(_production1Token));
        var resolver = new OperatorScopeConflictResolver(operators, _objectMap, true);

        LrConflictResolverDecision decision = resolver.ResolveShiftReduceConflict(_terminal1Handle, _production1Handle);

        Assert.That(decision, Is.EqualTo(expectedDecision));
    }

    [Test]
    [TestCase(AssociativityType.LeftAssociative, LrConflictResolverDecision.ChooseOption2)]
    [TestCase(AssociativityType.RightAssociative, LrConflictResolverDecision.ChooseOption1)]
    [TestCase(AssociativityType.NonAssociative, LrConflictResolverDecision.ChooseNeither)]
    [TestCase(AssociativityType.PrecedenceOnly, LrConflictResolverDecision.CannotChoose)]
    public void TestSamePrecedence(AssociativityType associativityType, LrConflictResolverDecision expectedDecision)
    {
        OperatorScope operators = new(new AssociativityGroup(associativityType, _terminal1, _production1Token));
        var resolver = new OperatorScopeConflictResolver(operators, _objectMap, true);

        LrConflictResolverDecision decision = resolver.ResolveShiftReduceConflict(_terminal1Handle, _production1Handle);

        Assert.That(decision, Is.EqualTo(expectedDecision));
    }

    [Test]
    [TestCase(-1, LrConflictResolverDecision.ChooseOption2)]
    [TestCase(0, LrConflictResolverDecision.CannotChoose)]
    [TestCase(1, LrConflictResolverDecision.ChooseOption1)]
    public void TestReduceReduceDifferentPrecedence(int order, LrConflictResolverDecision expectedDecision)
    {
        AssociativityGroup[] associativityGroups = order switch
        {
            < 0 => [new PrecedenceOnly(_production1Token), new PrecedenceOnly(_production2Token)],
            0 => [new PrecedenceOnly(_production1Token, _production2Token)],
            > 0 => [new PrecedenceOnly(_production2Token), new PrecedenceOnly(_production1Token)]
        };
        OperatorScope operators = new(true, associativityGroups);
        var resolver = new OperatorScopeConflictResolver(operators, _objectMap, true);

        LrConflictResolverDecision decision = resolver.ResolveReduceReduceConflict(_production1Handle, _production2Handle);

        Assert.Multiple(() =>
        {
            Assert.That(operators.CanResolveReduceReduceConflicts);
            Assert.That(decision, Is.EqualTo(expectedDecision));
        });
    }

    [Test]
    public void TestReduceReduceUnsupported()
    {
        OperatorScope operators = new(new PrecedenceOnly(_production1Token), new PrecedenceOnly(_production2Token));
        var resolver = new OperatorScopeConflictResolver(operators, _objectMap, true);

        LrConflictResolverDecision decision = resolver.ResolveReduceReduceConflict(_production1Handle, _production2Handle);

        Assert.Multiple(() =>
        {
            Assert.That(operators.CanResolveReduceReduceConflicts, Is.False);
            Assert.That(decision, Is.EqualTo(LrConflictResolverDecision.CannotChoose));
        });
    }

    [Test]
    public void TestLiterals([Values] bool caseSensitive)
    {
        const string literal1 = nameof(literal1);
        const string literal2 = nameof(literal2);
        OperatorScope operators = new(new PrecedenceOnly(literal1), new PrecedenceOnly(Terminal.Literal(literal2)));
        var objectMap = new Dictionary<EntityHandle, object>
        {
            [_terminal1Handle] = Terminal.Literal(caseSensitive ? literal1 : literal1.ToUpperInvariant()),
            [_production1Handle] = (caseSensitive ? literal2 : literal2.ToUpperInvariant()).Appended()
        };
        var resolver = new OperatorScopeConflictResolver(operators, objectMap, caseSensitive);

        LrConflictResolverDecision decision = resolver.ResolveShiftReduceConflict(_terminal1Handle, _production1Handle);

        Assert.That(decision, Is.EqualTo(LrConflictResolverDecision.ChooseOption2));
    }
}
