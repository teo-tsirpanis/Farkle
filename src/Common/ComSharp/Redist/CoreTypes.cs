// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

// This file contains core types for COM# interop.

// This file contains core types for the COM# interop system.

global using ComSharpVtable = System.Collections.Generic.IReadOnlyList<System.Delegate>;
global using ComSharpObject = (object? SourceObject, System.Collections.Generic.IReadOnlyList<System.Delegate> Vtable);

using Microsoft.CodeAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace ComSharp;

/// <summary>
/// Contains the logic to marshal a closed universe of COM# interfaces.
/// </summary>
[Embedded]
internal abstract class ComSharpWrappers
{
    private readonly ComSharpVtable _defaultVtable;

    public ComSharpVtable? QueryInterface(object? source, Guid iid)
    {
        if (source is null)
        {
            return null;
        }
        if (!TryGetInterfaceInfo(iid, out var type, out var vtable))
        {
            return null;
        }
        if (!type.IsAssignableFrom(source.GetType()))
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

    public abstract object? CreateObject(Type targetType, object sourceObject, ComSharpVtable vtable);

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
            case WrappedObject wrapped:
                return (wrapped.SourceObject, wrapped.Vtable);
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
        if (CreateObject(typeof(T), obj.SourceObject, obj.Vtable) is not T wrapped)
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
        if (source is WrappedObject wrapped)
        {
            return (wrapped.SourceObject, wrapped.Vtable);
        }
        return (source, _defaultVtable);
    }

    /// <summary>
    /// Creates a .NET object from a COM# object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned object supports being casted through methods in the <see cref="WrappedObjectExtensions"/>
    /// class, to any interface known to this <see cref="ComSharpWrappers"/> instance, that is implemented by
    /// <paramref name="obj"/>.
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
        return new WrappedObject(this, obj.SourceObject, obj.Vtable);
    }
}

[Embedded]
internal class WrappedObject(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable)
{
    protected ComSharpWrappers Wrappers { get; } = wrappers;

    public object SourceObject { get; } = sourceObject;

    public ComSharpVtable Vtable { get; } = vtable;

    private ComSharpVtable? QueryInterface(Guid iid) => ((Func<object?, Guid, ComSharpVtable?>)Vtable[0])(SourceObject, iid);

    public bool Is<T>([NotNullWhen(true)] out T? result) where T : class
    {
        result = null;
        if (QueryInterface(typeof(T).GUID) is not { } newVtable)
        {
            return false;
        }
        if (Wrappers.CreateObject(typeof(T), SourceObject, newVtable) is not T x)
        {
            return false;
        }
        result = x;
        return result is not null;
    }

    public override bool Equals(object? obj) => obj is WrappedObject other && Equals(SourceObject, other.SourceObject);

    public override int GetHashCode() => SourceObject.GetHashCode();

    public override string? ToString() => SourceObject.ToString();
}

[Embedded]
internal static class WrappedObjectExtensions
{
    public static bool IsComSharp<T>([NotNullWhen(true)] this object? obj, [NotNullWhen(true)] out T? result) where T : class
    {
        result = null;
        if (obj is not WrappedObject wrapped)
        {
            return false;
        }
        return wrapped.Is(out result);
    }

    [return: NotNullIfNotNull(nameof(obj))]
    public static T? AsComSharp<T>(this object? obj) where T : class
    {
        if (obj is null)
        {
            return null;
        }
        if (!obj.IsComSharp(out T? result))
        {
            throw new InvalidCastException();
        }
        return result;
    }
}
