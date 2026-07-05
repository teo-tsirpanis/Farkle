// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ComSharp;

partial class PrecompilerInterfaceWrappers
{
    public static PrecompilerInterfaceWrappers Instance { get; } = new();

    private static KeyValuePair<Guid, (Type, ComSharpVtable)> MakeKVP<T>(ComSharpVtable vtable)
        => new(typeof(T).GUID, (typeof(T), vtable));

    private Dictionary<Guid, (Type, ComSharpVtable)> KnownTypes => field ??= new([
        MakeKVP<IPrecompiledGrammar>(IPrecompiledGrammar_Vtable),
        MakeKVP<ILogger>(ILogger_Vtable),
        MakeKVP<IPrecompilerOptions>(IPrecompilerOptions_Vtable),
        MakeKVP<IPrecompilerInterface>(IPrecompilerInterface_Vtable),
    ]);

    private PrecompilerInterfaceWrappers() { }

    protected override bool TryGetInterfaceInfo(Guid iid, [NotNullWhen(true)] out Type? type, [NotNullWhen(true)] out ComSharpVtable? vtable)
    {
        if (KnownTypes.TryGetValue(iid, out var info))
        {
            (type, vtable) = info;
            return true;
        }
        type = null;
        vtable = null;
        return false;
    }

    protected override RuntimeTypeHandle GetDotNetWrapperImplementation(RuntimeTypeHandle interfaceType)
    {
        var type = Type.GetTypeFromHandle(interfaceType);
        if (type == typeof(IPrecompiledGrammar)) return typeof(IPrecompiledGrammar_Wrapper).TypeHandle;
        if (type == typeof(ILogger)) return typeof(ILogger_Wrapper).TypeHandle;
        if (type == typeof(IPrecompilerOptions)) return typeof(IPrecompilerOptions_Wrapper).TypeHandle;
        if (type == typeof(IPrecompilerInterface)) return typeof(IPrecompilerInterface_Wrapper).TypeHandle;
        return default;
    }

    protected override DotNetWrapper CreateDotNetWrapper(object sourceObject, ComSharpVtable vtable)
        => new Wrapper(this, sourceObject, vtable);

    private sealed class Wrapper(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable)
        : DotNetWrapper(wrappers, sourceObject, vtable);
}

#region COM# callable wrappers
partial class PrecompilerInterfaceWrappers
{
    private static string? IPrecompiledGrammar_get_Key(object obj) => ((IPrecompiledGrammar)obj).Key;
    private static byte[]? IPrecompiledGrammar_get_GrammarFile(object obj) => ((IPrecompiledGrammar)obj).GrammarFile;
    private static int IPrecompiledGrammar_get_InputMethodMetadataToken(object obj) => ((IPrecompiledGrammar)obj).InputMethodMetadataToken;
    private static IReadOnlyList<(int, int)> IPrecompiledGrammar_get_OutputMethods(object obj) =>
        CollectionMarshaller.Marshal(((IPrecompiledGrammar)obj).OutputMethods, x => (x.MetadataToken, (int)x.Type));

    private ComSharpVtable IPrecompiledGrammar_Vtable => field ??= [
        QueryInterface,
        IPrecompiledGrammar_get_Key,
        IPrecompiledGrammar_get_GrammarFile,
        IPrecompiledGrammar_get_InputMethodMetadataToken,
        IPrecompiledGrammar_get_OutputMethods,
    ];
}

partial class PrecompilerInterfaceWrappers
{
    private static int ILogger_get_LogLevel(object obj) => (int)((ILogger)obj).LogLevel;
    private void ILogger_Log(object obj, int severity, object message, string code)
        => ((ILogger)obj).Log((DiagnosticSeverity)severity, message, code);

    private ComSharpVtable ILogger_Vtable => field ??= [
        QueryInterface,
        ILogger_get_LogLevel,
        ILogger_Log,
    ];
}

partial class PrecompilerInterfaceWrappers
{
    private static CancellationToken IPrecompilerOptions_get_CancellationToken(object obj) => ((IPrecompilerOptions)obj).CancellationToken;
    private ComSharpObject IPrecompilerOptions_get_Logger(object obj) => Marshal(((IPrecompilerOptions)obj).Logger);

    private ComSharpVtable IPrecompilerOptions_Vtable => field ??= [
        QueryInterface,
        IPrecompilerOptions_get_CancellationToken,
        IPrecompilerOptions_get_Logger,
    ];
}

partial class PrecompilerInterfaceWrappers
{
    private IEnumerable<ComSharpObject> IPrecompilerInterface_DiscoverAndPrecompile(object obj, IReadOnlyCollection<Type> types, ComSharpObject options) =>
        CollectionMarshaller.Marshal(((IPrecompilerInterface)obj).DiscoverAndPrecompile(types, Unmarshal<IPrecompilerOptions>(options)), Marshal);

    private ComSharpVtable IPrecompilerInterface_Vtable => field ??= [
        QueryInterface,
        IPrecompilerInterface_DiscoverAndPrecompile,
    ];
}
#endregion

#region .NET callable wrappers
#pragma warning disable CA2256 // All members declared in parent interfaces must have an implementation in a DynamicInterfaceCastableImplementation-attributed interface
// We get the implementation of IDotNetWrapper from the .NET wrapper object.
[DynamicInterfaceCastableImplementation]
file interface IPrecompiledGrammar_Wrapper : IPrecompiledGrammar, IDotNetWrapper
{
    string? IPrecompiledGrammar.Key =>
        this.GetFunction<IPrecompiledGrammar, Func<object, string?>>(1)(SourceObject);

    byte[]? IPrecompiledGrammar.GrammarFile =>
        this.GetFunction<IPrecompiledGrammar, Func<object, byte[]?>>(2)(SourceObject);

    int IPrecompiledGrammar.InputMethodMetadataToken =>
        this.GetFunction<IPrecompiledGrammar, Func<object, int>>(3)(SourceObject);

    IReadOnlyList<(int MetadataToken, OutputType Type)> IPrecompiledGrammar.OutputMethods =>
        CollectionMarshaller.Marshal(this.GetFunction<IPrecompiledGrammar, Func<object, IReadOnlyList<(int, int)>>>(4)(SourceObject), x => (x.Item1, (OutputType)x.Item2));
}

[DynamicInterfaceCastableImplementation]
file interface ILogger_Wrapper : ILogger, IDotNetWrapper
{
    DiagnosticSeverity ILogger.LogLevel =>
        (DiagnosticSeverity)this.GetFunction<ILogger, Func<object, int>>(1)(SourceObject);

    void ILogger.Log(DiagnosticSeverity severity, object message, string? code) =>
        this.GetFunction<ILogger, Action<object, int, object, string?>>(2)(SourceObject, (int)severity, message, code);
}

[DynamicInterfaceCastableImplementation]
file interface IPrecompilerOptions_Wrapper : IPrecompilerOptions, IDotNetWrapper
{
    CancellationToken IPrecompilerOptions.CancellationToken =>
        this.GetFunction<IPrecompilerOptions, Func<object, CancellationToken>>(1)(SourceObject);

    ILogger IPrecompilerOptions.Logger =>
        Wrappers.Unmarshal<ILogger>(this.GetFunction<IPrecompilerOptions, Func<object, ComSharpObject>>(2)(SourceObject));
}

[DynamicInterfaceCastableImplementation]
file interface IPrecompilerInterface_Wrapper : IPrecompilerInterface, IDotNetWrapper
{
    IEnumerable<IPrecompiledGrammar> IPrecompilerInterface.DiscoverAndPrecompile(IReadOnlyCollection<Type> types, IPrecompilerOptions? options) =>
        CollectionMarshaller.Marshal(this.GetFunction<IPrecompilerInterface, Func<object, IReadOnlyCollection<Type>, ComSharpObject, IEnumerable<ComSharpObject>>>(1)(SourceObject, types, Wrappers.Marshal(options)), Wrappers.Unmarshal<IPrecompiledGrammar>);
}
#pragma warning restore CA2256 // All members declared in parent interfaces must have an implementation in a DynamicInterfaceCastableImplementation-attributed interface
#endregion
