// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Farkle.Analyzers.Tests;

public sealed class NUnitVerifier : DefaultVerifier
{
    public NUnitVerifier() : base([]) { }

    public NUnitVerifier(ImmutableStack<string> context) : base(context) { }

    public override void Empty<T>(string collectionName, IEnumerable<T> collection) =>
        Assert.That(collection, Is.Empty, actualExpression: collectionName);

    public override void Equal<T>(T expected, T actual, string? message = null) =>
        Assert.That(actual, Is.EqualTo(expected), CreateMessage(message ?? ""));

    [DoesNotReturn]
#pragma warning disable CS8763 // A method marked [DoesNotReturn] should not return.
    public override void Fail(string? message = null) =>
        Assert.Fail(CreateMessage(message ?? ""));
#pragma warning restore CS8763 // A method marked [DoesNotReturn] should not return.

    public override void False([DoesNotReturnIf(true)] bool assert, string? message = null) =>
        Assert.That(assert, Is.False, CreateMessage(message ?? ""));

    public override void LanguageIsSupported(string language) =>
        Assert.That(language, Is.EqualTo(LanguageNames.CSharp).Or.EqualTo(LanguageNames.VisualBasic));

    public override void NotEmpty<T>(string collectionName, IEnumerable<T> collection) =>
        Assert.That(collection, Is.Not.Empty, actualExpression: collectionName);

    public override IVerifier PushContext(string context) => new NUnitVerifier(Context.Push(context));

    public override void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, IEqualityComparer<T>? equalityComparer = null, string? message = null) =>
        Assert.That(actual, Is.EqualTo(expected).Using(equalityComparer ?? EqualityComparer<T>.Default), CreateMessage(message ?? ""));

    public override void True([DoesNotReturnIf(false)] bool assert, string? message = null) =>
        Assert.That(assert, Is.True, CreateMessage(message ?? ""));
}
