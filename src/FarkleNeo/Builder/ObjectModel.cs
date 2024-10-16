// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// This file contains the base definitions of the builder's object model.

using System.Collections.Immutable;

namespace Farkle.Builder;

/// <summary>
/// Represents a grammar to be built, containing syntax and semantic rules.
/// </summary>
/// <remarks>
/// <para>
/// This is the base interface of the object model of Farkle's builder
/// and cannot be implemented by user code.
/// </para>
/// <para>
/// The only operations allowed on grammar builders are building a grammar and
/// setting options global to the grammar.
/// </para>
/// </remarks>
/// <seealso cref="GrammarBuilderExtensions"/>
public interface IGrammarBuilder
{
    // TODO-NS2.0: Change this to return IGrammarSymbol, and add a property to IGrammarSymbol to
    // return ISymbolBase. We could do it now, but it will introduce lots of duplication because
    // we would have to add two properties for each class. Better do it when we target exclusively
    // frameworks that support DIMs.
    internal ISymbolBase Symbol { get; }
}

/// <summary>
/// Augments <see cref="IGrammarBuilder"/> to represent a grammar to be built
/// with a known type of values it produces.
/// </summary>
/// <typeparam name="T">The type of values the grammar will produce.</typeparam>
/// <remarks>
/// This interface cannot be implemented by user code.
/// </remarks>
/// <seealso cref="GrammarBuilderExtensions"/>
public interface IGrammarBuilder<out T> : IGrammarBuilder;

/// <summary>
/// Represents a symbol (terminal or nonterminal) in a grammar to be built that can be
/// composed to form more complex symbols.
/// </summary>
/// <remarks>
/// <para>
/// This interface inherits from <see cref="IGrammarBuilder"/> and also represents a
/// grmmar to be built with this symbol as its start symbol. It cannot be implemented
/// by user code.
/// </para>
/// <para>
/// This interface is the closest replacement of Farkle 6's <c>DesigntimeFarkle</c>
/// interface. The functionality of that interface has been split to this interface
/// and <see cref="IGrammarBuilder"/>, to codify in the type system which configuration
/// operations apply to individual symbols and which apply to the whole grammar.
/// </para>
/// </remarks>
/// <seealso cref="GrammarSymbolExtensions"/>
public interface IGrammarSymbol : IGrammarBuilder
{
    /// <summary>
    /// The symbol's name.
    /// </summary>
    /// <remarks>
    /// This value is used only for diagnostic and documentation purposes and does
    /// not affect the grammar's behavior when parsing. A grammar may contain multiple
    /// symbols with the same name.
    /// </remarks>
    string Name { get; }
}

/// <summary>
/// Combines <see cref="IGrammarSymbol"/> and <see cref="IGrammarBuilder{T}"/> to represent
/// a symbol in a grammar to be built with a known type of values it produces.
/// </summary>
/// <typeparam name="T">The type of values the symbol will produce.</typeparam>
/// <remarks>
/// <para>
/// This interface cannot be implemented by user code.
/// </para>
/// <para>
/// This interface is the closest replacement of Farkle 6's <c>DesigntimeFarkle&lt;TResult&gt;</c>
/// interface. The functionality of that interface has been split to this interface
/// and <see cref="IGrammarBuilder"/>, to codify in the type system which configuration
/// operations apply to individual symbols and which apply to the whole grammar.
/// </para>
/// </remarks>
/// <seealso cref="GrammarSymbolExtensions"/>
/// <seealso cref="IGrammarBuilder"/>
public interface IGrammarSymbol<out T> : IGrammarBuilder<T>, IGrammarSymbol;

/// <summary>
/// Marker interface for the types of concrete symbols in a grammar to be built,
/// as opposed to wrapper classes that change configuration options.
/// </summary>
internal interface ISymbolBase : IGrammarSymbol;

// Terminal also contains public factory methods so it is public and partial.
// No public API must return objects of type Terminal, only IGrammarSymbol(<T>).
public partial class Terminal : ISymbolBase
{
    internal string Name { get; }

    internal Regex Regex { get; }

    internal Transformer<char, object?> Transformer { get; }

    internal TerminalOptions Options { get; }

    internal Terminal(string name, Regex regex, Transformer<char, object?> transformer, TerminalOptions options)
    {
        Name = name;
        Regex = regex;
        Transformer = transformer;
        Options = options;
    }

    string IGrammarSymbol.Name => Name;

    ISymbolBase IGrammarBuilder.Symbol => this;
}

internal sealed class VirtualTerminal(string name, TerminalOptions options) : ISymbolBase
{
    public string Name { get; } = name;

    public TerminalOptions Options { get; } = options;

    ISymbolBase IGrammarBuilder.Symbol => this;
}

internal sealed class Literal(string value) : ISymbolBase
{
    public string Name => Value;

    public string Value { get; } = value;

    ISymbolBase IGrammarBuilder.Symbol => this;
}

internal sealed class NewLine : ISymbolBase
{
    public static NewLine Instance { get; } = new();

    public string Name => nameof(NewLine);

    ISymbolBase IGrammarBuilder.Symbol => this;

    private NewLine() { }
}

// Group is a similar case to Terminal, so it is public and partial.
// No public API must return objects of type Group, only IGrammarSymbol(<T>).
public abstract partial class Group : ISymbolBase
{
    internal string Name { get; }

    internal string GroupStart { get; }

    internal Transformer<char, object?> Transformer { get; }

    internal GroupOptions Options { get; }

    private protected Group(string name, string groupStart, Transformer<char, object?> transformer, GroupOptions options)
    {
        Name = name;
        GroupStart = groupStart;
        Transformer = transformer;
        Options = options;
    }

    string IGrammarSymbol.Name => Name;

    ISymbolBase IGrammarBuilder.Symbol => this;
}

internal class LineGroup(string name, string groupStart, Transformer<char, object?> transformer, GroupOptions options) : Group(name, groupStart, transformer, options);

internal class BlockGroup(string name, string groupStart, string groupEnd, Transformer<char, object?> transformer, GroupOptions options) : Group(name, groupStart, transformer, options)
{
    public string GroupEnd { get; } = groupEnd;
}

internal interface IProduction
{
    ImmutableArray<IGrammarSymbol> Members { get; }

    Fuser<object?> Fuser { get; }

    object? PrecedenceToken { get; }
}

// This is an interface because both Nonterminal and Nonterminal<T>
// must be public, and the former cannot inherit from the latter.
internal interface INonterminal : ISymbolBase
{
    ImmutableArray<IProduction> FreezeAndGetProductions();
}

internal sealed class Terminal<T>(string name, Regex regex, Transformer<char, object?> transformer, TerminalOptions options) : Terminal(name, regex, transformer, options), IGrammarSymbol<T>;

internal sealed class LineGroup<T>(string name, string groupStart, Transformer<char, object?> transformer, GroupOptions options) : LineGroup(name, groupStart, transformer, options), IGrammarSymbol<T>;

internal sealed class BlockGroup<T>(string name, string groupStart, string groupEnd, Transformer<char, object?> transformer, GroupOptions options) : BlockGroup(name, groupStart, groupEnd, transformer, options), IGrammarSymbol<T>;

/// <summary>
/// Represents a production in a grammar to be built that produces a value.
/// </summary>
/// <typeparam name="T">The type of values the production will produce.</typeparam>
/// <remarks>
/// This interface cannot be implemented by user code.
/// </remarks>
/// <seealso cref="Nonterminal.Create{T}(string, IProduction{T}[])"/>
/// <seealso cref="Nonterminal{T}.SetProductions(IProduction{T}[])"/>
/// <seealso cref="Nonterminal{T}.SetProductions(ReadOnlySpan{IProduction{T}})"/>
public interface IProduction<out T>
{
    // We cannot inherit IProduction because we want the generic interface to be public.
    // Instead, we expose the IProduction through this property.
    internal IProduction Production { get; }
}

internal class Production<T>(ImmutableArray<IGrammarSymbol> symbols, Fuser<object?> fuser, object? precedenceToken) : IProduction, IProduction<T>
{
    public ImmutableArray<IGrammarSymbol> Members { get; } = symbols;

    public Fuser<object?> Fuser { get; } = fuser;

    public object? PrecedenceToken { get; } = precedenceToken;

    IProduction IProduction<T>.Production => this;
}
