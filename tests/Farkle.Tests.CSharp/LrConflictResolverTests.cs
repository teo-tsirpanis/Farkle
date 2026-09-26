// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Farkle.Builder;
using Farkle.Builder.Lr;
using Farkle.Builder.OperatorPrecedence;
using Farkle.Grammars;
using static Farkle.Builder.Lr.AugmentedSyntaxProvider;

namespace Farkle.Tests.CSharp;

internal class LrConflictResolverTests
{
    private static readonly IGrammarSymbol _terminal1 = Terminal.Virtual(nameof(_terminal1));

    private static readonly TokenSymbolHandle _terminal1Handle = new(1);

    private static readonly Symbol _terminal1Symbol = Symbol.CreateTerminal(1, default);

    private static readonly object _production1Token = new();

    private static readonly IProduction _production1 = new ProductionBuilder(_terminal1).WithPrecedence(_production1Token);

    private static readonly ProductionHandle _production1Handle = new(1);

    private static readonly object _production2Token = new();

    private static readonly IProduction _production2 = new ProductionBuilder(_terminal1).WithPrecedence(_production2Token);

    private static readonly ProductionHandle _production2Handle = new(2);

    private static readonly LrConflictContribution _shiftContribution = LrConflictContribution.CreateShift(0);

    private static readonly LrConflictContribution _reduceContribution1 = LrConflictContribution.CreateReduce(new(1, default));

    private static readonly LrConflictContribution _reduceContribution2 = LrConflictContribution.CreateReduce(new(2, default));

    private static readonly Dictionary<EntityHandle, object> _objectMap = new()
    {
        [_terminal1Handle] = _terminal1,
        [_production1Handle] = _production1,
        [_production2Handle] = _production2
    };

    [Test]
    [TestCaseSource(nameof(GetTestCases))]
    public void Test(OperatorScope operators, LrConflictContribution[] contributions, LrConflictContribution[]? expectedContributions)
    {
        var resolved = ResolveConflicts(operators, contributions);
        Assert.That(resolved, Is.EqualTo(expectedContributions));
    }

    private static TestCaseData<OperatorScope, LrConflictContribution[], LrConflictContribution[]?>[] GetTestCases()
    {
        LrConflictContribution[] shiftReduce = [_shiftContribution, _reduceContribution1];
        LrConflictContribution[] shiftReduceReduce = [_shiftContribution, _reduceContribution1, _reduceContribution2];

        return [
            new([new LeftAssociative(_production1Token), new LeftAssociative(_terminal1)], shiftReduce, [_shiftContribution]) { TestName = "DifferentPrecedence_1" },
            new([new LeftAssociative(_terminal1), new LeftAssociative(_production1Token)], shiftReduce, [_reduceContribution1]) { TestName = "DifferentPrecedence_2" },
            MakeSamePrecedence(AssociativityType.LeftAssociative, [_reduceContribution1]),
            MakeSamePrecedence(AssociativityType.RightAssociative, [_shiftContribution]),
            MakeSamePrecedence(AssociativityType.NonAssociative, []),
            MakeSamePrecedence(AssociativityType.PrecedenceOnly, [_shiftContribution, _reduceContribution1]),
            MakeSamePrecedenceMulti(AssociativityType.LeftAssociative, [_reduceContribution1, _reduceContribution2]),
            MakeSamePrecedenceMulti(AssociativityType.RightAssociative, [_shiftContribution]),
            MakeSamePrecedenceMulti(AssociativityType.NonAssociative, []),
            MakeSamePrecedenceMulti(AssociativityType.PrecedenceOnly, [_shiftContribution, _reduceContribution1, _reduceContribution2]),
            MakeReduceReduceDifferentPrecedence(1, [new(_production1Token), new(_production2Token)], [_reduceContribution2]),
            MakeReduceReduceDifferentPrecedence(2, [new(_production1Token, _production2Token)], [_reduceContribution1, _reduceContribution2]),
            MakeReduceReduceDifferentPrecedence(3, [new(_production2Token), new(_production1Token)], [_reduceContribution1]),
            new([new PrecedenceOnly(_production1Token), new PrecedenceOnly(_production2Token)], [_reduceContribution1, _reduceContribution2], null) { TestName = "ReduceReduceUnsupported" },
        ];

        TestCaseData<OperatorScope, LrConflictContribution[], LrConflictContribution[]?> MakeSamePrecedence
            (AssociativityType associativityType, LrConflictContribution[]? expectedContributions) =>
            new([new AssociativityGroup(associativityType, _terminal1, _production1Token)], shiftReduce, expectedContributions)
            {
                TestName = $"SamePrecedence_{associativityType}"
            };

        TestCaseData<OperatorScope, LrConflictContribution[], LrConflictContribution[]?> MakeSamePrecedenceMulti
            (AssociativityType associativityType, LrConflictContribution[]? expectedContributions) =>
            new([new AssociativityGroup(associativityType, _terminal1, _production1Token, _production2Token)], shiftReduceReduce, expectedContributions)
            {
                TestName = $"SamePrecedenceMulti_{associativityType}"
            };

        TestCaseData<OperatorScope, LrConflictContribution[], LrConflictContribution[]?> MakeReduceReduceDifferentPrecedence
            (int id, ImmutableArray<PrecedenceOnly> associativityGroups, LrConflictContribution[]? expectedContributions) =>
            new(new(true, ImmutableArray<AssociativityGroup>.CastUp(associativityGroups)), [_reduceContribution1, _reduceContribution2], expectedContributions)
            {
                TestName = $"ReduceReduceDifferentPrecedence_{id}"
            };
    }

    private static List<LrConflictContribution>? ResolveConflicts(OperatorScope operatorScope, LrConflictContribution[] contributions)
    {
        var infoProvider = new OperatorInfoProvider(operatorScope, _objectMap, EqualityComparer<object>.Default);
        var conflictsSinglePass = ResolveConflictsSinglePass();
        var conflictsMultiPass = ResolveConflictsMultiPass();
        Assert.That(conflictsSinglePass, Is.EqualTo(conflictsMultiPass));
        return conflictsSinglePass;

        List<LrConflictContribution>? ResolveConflictsSinglePass()
        {
            var result = new List<LrConflictContribution>();
            var conflictResolver = new LrConflictResolver(infoProvider);
            foreach (var contribution in contributions)
            {
                switch (conflictResolver.Add(_terminal1Symbol, contribution))
                {
                    case LrConflictResolverDecision.Ignore:
                        break;
                    case LrConflictResolverDecision.AddToDominantSet:
                        result.Add(contribution);
                        break;
                    case LrConflictResolverDecision.CreateNewDominantSet:
                        result.Clear();
                        result.Add(contribution);
                        break;
                    case LrConflictResolverDecision.ClearDominantSet:
                        result.Clear();
                        break;
                    case LrConflictResolverDecision.NoPrecedence:
                        return null;
                }
            }
            return result;
        }

        List<LrConflictContribution>? ResolveConflictsMultiPass()
        {
            var result = new List<LrConflictContribution>();
            var precedences = new List<int>();
            var conflictResolver = new LrConflictResolver(infoProvider);
            foreach (var contribution in contributions)
            {
                _ = conflictResolver.Add(_terminal1Symbol, contribution, out int precedence);
                if (precedence < 0)
                {
                    return null;
                }
                precedences.Add(precedence);
            }
            for (int i = 0; i < precedences.Count; i++)
            {
                if (conflictResolver.ContainsInDominantSet(contributions[i], precedences[i]))
                {
                    result.Add(contributions[i]);
                }
            }
            return result;
        }
    }
}
