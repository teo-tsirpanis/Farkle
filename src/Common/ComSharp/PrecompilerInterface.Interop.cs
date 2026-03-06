// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

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

    public override object? CreateObject(Type targetType, object sourceObject, ComSharpVtable vtable)
    {
        if (targetType == typeof(IPrecompiledGrammar)) return new IPrecompiledGrammar_Wrapper(this, sourceObject, vtable);
        if (targetType == typeof(ILogger)) return new ILogger_Wrapper(this, sourceObject, vtable);
        if (targetType == typeof(IPrecompilerOptions)) return new IPrecompilerOptions_Wrapper(this, sourceObject, vtable);
        if (targetType == typeof(IPrecompilerInterface)) return new IPrecompilerInterface_Wrapper(this, sourceObject, vtable);
        return null;
    }
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
file sealed class IPrecompiledGrammar_Wrapper(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable)
    : WrappedObject(wrappers, sourceObject, vtable), IPrecompiledGrammar
{
    private readonly Func<object, string?> f_Key = (Func<object, string?>)vtable[1];
    private readonly Func<object, byte[]?> f_GrammarFile = (Func<object, byte[]?>)vtable[2];
    private readonly Func<object, int> f_InputMethodMetadataToken = (Func<object, int>)vtable[3];
    private readonly Func<object, IReadOnlyList<(int, int)>> f_OutputMethods = (Func<object, IReadOnlyList<(int, int)>>)vtable[4];

    public string? Key => f_Key(SourceObject);
    public byte[]? GrammarFile => f_GrammarFile(SourceObject);
    public int InputMethodMetadataToken => f_InputMethodMetadataToken(SourceObject);
    public IReadOnlyList<(int MetadataToken, OutputType Type)> OutputMethods =>
        CollectionMarshaller.Marshal(f_OutputMethods(SourceObject), x => (x.Item1, (OutputType)x.Item2));
}

file sealed class ILogger_Wrapper(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable) :
    WrappedObject(wrappers, sourceObject, vtable), ILogger
{
    private readonly Func<object, int> f_LogLevel = (Func<object, int>)vtable[1];
    private readonly Action<object, int, object, string?> f_Log = (Action<object, int, object, string?>)vtable[2];

    public DiagnosticSeverity LogLevel => (DiagnosticSeverity)f_LogLevel(SourceObject);
    public void Log(DiagnosticSeverity severity, object message, string? code) => f_Log(SourceObject, (int)severity, message, code);
}

file sealed class IPrecompilerOptions_Wrapper(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable)
    : WrappedObject(wrappers, sourceObject, vtable), IPrecompilerOptions
{
    private readonly Func<object, CancellationToken> f_CancellationToken = (Func<object, CancellationToken>)vtable[1];
    private readonly Func<object, ComSharpObject> f_Logger = (Func<object, ComSharpObject>)vtable[2];

    public CancellationToken CancellationToken => f_CancellationToken(SourceObject);
    public ILogger Logger => Wrappers.Unmarshal<ILogger>(f_Logger(SourceObject));
}

file sealed class IPrecompilerInterface_Wrapper(ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable)
    : WrappedObject(wrappers, sourceObject, vtable), IPrecompilerInterface
{
    private readonly Func<object, IReadOnlyCollection<Type>, ComSharpObject, IEnumerable<ComSharpObject>> f_DiscoverAndPrecompile = (Func<object, IReadOnlyCollection<Type>, ComSharpObject, IEnumerable<ComSharpObject>>)vtable[1];

    public IEnumerable<IPrecompiledGrammar> DiscoverAndPrecompile(IReadOnlyCollection<Type> types, IPrecompilerOptions? options) =>
        CollectionMarshaller.Marshal(f_DiscoverAndPrecompile(SourceObject, types, Wrappers.Marshal(options)), Wrappers.Unmarshal<IPrecompiledGrammar>);
}
#endregion
