// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// This file contains core types for COM# interop.

global using ComSharpVtable = System.Collections.Generic.IReadOnlyList<System.Delegate>;
global using ComSharpObject = (object? SourceObject, System.Collections.Generic.IReadOnlyList<System.Delegate> Vtable);

using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ComSharp;

/// <summary>
/// Contains the logic to marshal a closed universe of COM# interfaces.
/// </summary>
[Embedded]
internal abstract class ComSharpWrappers
{
    private readonly ComSharpVtable _defaultVtable;

    protected ComSharpVtable? QueryInterface(object? source, Guid iid)
    {
        if (source is null)
        {
            return null;
        }
        if (!TryGetInterfaceInfo(iid, out var type, out var vtable))
        {
            return null;
        }
        if (!type.IsInstanceOfType(source))
        {
            return null;
        }
        return vtable;
    }

    protected ComSharpWrappers()
    {
        _defaultVtable = [QueryInterface];
    }

    protected abstract bool TryGetInterfaceInfo(Guid iid, [NotNullWhen(true)] out Type? type, [NotNullWhen(true)] out ComSharpVtable? vtable);

    protected abstract RuntimeTypeHandle GetDotNetWrapperImplementation(RuntimeTypeHandle interfaceType);

    protected abstract DotNetWrapper CreateDotNetWrapper(object sourceObject, ComSharpVtable vtable);

    // Because COM# objects are value tuples, they can't work with nullable reference types.
    // In order to avoid !s in marshalling code, mark method signatures as nullable-oblivious.
#nullable disable annotations

    /// <summary>
    /// Converts a .NET interface object to a COM# object with the vtable for the exact type.
    /// </summary>
    /// <remarks>
    /// This interface is intended to be used when marshalling arguments for a wrapper.
    /// User code is recommended to use <see cref="ConvertToComSharp"/> instead.
    /// </remarks>
    public ComSharpObject Marshal<T>(T source) where T : class
    {
        switch (source)
        {
            case null:
                return (null, _defaultVtable);
            case DotNetWrapper wrapped:
                return (wrapped.SourceObject, wrapped.QueryInterface(typeof(T).GUID)!);
        }
        if (!TryGetInterfaceInfo(typeof(T).GUID, out _, out var vtable))
        {
            throw new InvalidOperationException("Cannot marshal this interface to COM#");
        }
        return (source, vtable);
    }

    /// <summary>
    /// Converts a COM# object with the vtable for a particular interface type, to a .NET object of the exact type.
    /// </summary>
    /// <remarks>
    /// This interface is intended to be used when marshalling arguments for a wrapper.
    /// User code is recommended to use <see cref="ConvertToDotNet"/> instead.
    /// </remarks>
    public T Unmarshal<T>(ComSharpObject obj) where T : class
    {
        switch (obj.SourceObject)
        {
            case null: return null;
            case T x: return x;
        }
        if (CreateDotNetWrapper(obj.SourceObject, obj.Vtable) is not T wrapped)
        {
            throw new InvalidOperationException("Cannot create .NET wrapper for this interface");
        }
        return wrapped;
    }

#nullable restore annotations

    /// <summary>
    /// Creates a COM# object from a .NET object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned COM# object's query interface function supports all interfaces known to this
    /// <see cref="ComSharpWrappers"/> instance, that is implemented by <paramref name="source"/>.
    /// </para>
    /// <para>
    /// This method is the "exit point" from the .NET to the COM# type systems. It is intended to be called
    /// at the start of an interaction, with the returned COM# object being returned to consumers via reflection.
    /// </para>
    /// </remarks>
    public ComSharpObject ConvertToComSharp(object? source)
    {
        return ((source as DotNetWrapper)?.SourceObject ?? source, _defaultVtable);
    }

    /// <summary>
    /// Creates a .NET object from a COM# object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned object supports being casted to any interface known to this <see cref="ComSharpWrappers"/>
    /// instance, that is implemented by <paramref name="obj"/>.
    /// </para>
    /// <para>
    /// This method is the "entry point" from the COM# to the .NET type systems. It is intended to be called
    /// at the start of an interaction, with a COM# object that was obtained via reflection.
    /// </para>
    /// </remarks>
    public object? ConvertToDotNet(ComSharpObject obj)
    {
        if (obj.SourceObject is null)
        {
            return null;
        }
        return CreateDotNetWrapper(obj.SourceObject, obj.Vtable);
    }

    // This is an abstract class. Each ComSharpWrappers universe will return its own subclass from
    // CreateDotNetWrapper, in order to avoid interference in the runtime's IDIC cache across universes.
    protected abstract class DotNetWrapper(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable) : IDynamicInterfaceCastable, IDotNetWrapper
    {
        public ComSharpWrappers Wrappers { get; } = wrappers;

        public object SourceObject { get; } = sourceObject;

        private readonly Func<object?, Guid, ComSharpVtable?> _queryInterfaceFunc = (Func<object?, Guid, ComSharpVtable?>)vtable[0];

        public ComSharpVtable? QueryInterface(Guid iid) => _queryInterfaceFunc(SourceObject, iid);

        bool IDynamicInterfaceCastable.IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
        {
            var guid = Type.GetTypeFromHandle(interfaceType)!.GUID;
            bool isImplemented = QueryInterface(guid) is not null;
            if (!isImplemented && throwIfNotImplemented)
            {
                ThrowIfNotImplemented();
            }
            return isImplemented;

            [StackTraceHidden]
            static void ThrowIfNotImplemented()
            {
                throw new InvalidCastException();
            }
        }

        RuntimeTypeHandle IDynamicInterfaceCastable.GetInterfaceImplementation(RuntimeTypeHandle interfaceType) =>
            Wrappers.GetDotNetWrapperImplementation(interfaceType);

        public override bool Equals(object? obj) => Equals(SourceObject, (obj as DotNetWrapper)?.SourceObject ?? obj);

        public override int GetHashCode() => SourceObject.GetHashCode();

        public override string? ToString() => SourceObject.ToString();
    }
}

[Embedded]
internal interface IDotNetWrapper
{
    ComSharpWrappers Wrappers { get; }

    object SourceObject { get; }

    ComSharpVtable? QueryInterface(Guid iid);
}

[Embedded]
internal static class DotNetWrapperExtensions
{
    extension(IDotNetWrapper provider)
    {
        public TDelegate GetFunction<TInterface, TDelegate>(int idx)
            where TInterface : class
            where TDelegate : Delegate
        {
            return (TDelegate)provider.QueryInterface(typeof(TInterface).GUID)![idx];
        }
    }
}
