// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;

namespace ComSharp;

public partial class ComSharpSourceGenerator
{
    private static void EmitComSharpWrappers(IndentedTextWriter writer, ComSharpWrappersInfo wrappersInfo, CancellationToken cancellationToken)
    {
        wrappersInfo.Type.OpenWriteTo(writer);
        writer.WriteLine($"public static {wrappersInfo.Type.Name} Instance {{ get; }} = new();");
        writer.InnerWriter.WriteLine();
        writer.WriteLine("protected override global::System.Collections.Generic.Dictionary<global::System.Guid, (global::System.Type Type, ComSharpVtable Vtable)> KnownComSharpWrappers => field ??= new([");
        writer.Indent++;
        foreach (var @interface in wrappersInfo.Interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((@interface.Options & ComSharpInterfaceOptionsSpec.ComSharpWrapper) == 0)
            {
                continue;
            }
            writer.WriteLine($"MakeComSharpKey<{@interface.DisplayName}>(global::ComSharp.Generated.GeneratedComSharpWrappers.{SymbolFormat.GetComSharpWrapperName(@interface.DisplayName)}(this)),");
        }
        writer.Indent--;
        writer.WriteLine("]);");
        writer.InnerWriter.WriteLine();
        writer.WriteLine("protected override global::System.Collections.Generic.Dictionary<global::System.RuntimeTypeHandle, global::System.RuntimeTypeHandle> KnownDotNetWrappers => field ??= new([");
        writer.Indent++;
        foreach (var @interface in wrappersInfo.Interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((@interface.Options & ComSharpInterfaceOptionsSpec.DotNetWrapper) == 0)
            {
                continue;
            }
            writer.WriteLine($"MakeDotNetKey<{@interface.DisplayName}, global::ComSharp.Generated.{SymbolFormat.GetDotNetWrapperName(@interface.DisplayName)}>(),");
        }
        writer.Indent--;
        writer.WriteLine("]);");
        writer.InnerWriter.WriteLine();
        writer.WriteLine("protected override DotNetWrapper CreateDotNetWrapper(object sourceObject, ComSharpVtable vtable) => new Wrapper(this, sourceObject, vtable);");
        writer.InnerWriter.WriteLine();
        writer.WriteLine("private sealed class Wrapper(global::ComSharp.ComSharpWrappers wrappers, object sourceObject, ComSharpVtable vtable) : DotNetWrapper(wrappers, sourceObject, vtable);");
        wrappersInfo.Type.CloseWriteTo(writer);
    }

    private static void EmitWrapper(IndentedTextWriter writer, InterfaceInfo interfaceInfo, bool isComSharpWrapper, CancellationToken cancellationToken)
    {
        if (isComSharpWrapper)
        {
            writer.WriteLine($"public static global::System.Collections.Generic.IReadOnlyList<global::System.Delegate> {SymbolFormat.GetComSharpWrapperName(interfaceInfo.Name)}(global::ComSharp.ComSharpWrappers wrappers) => ");
            writer.WriteLine('[');
            writer.Indent++;
            writer.WriteLine("wrappers.QueryInterface,");
        }
        else
        {
            writer.WriteLine("[global::System.Runtime.InteropServices.DynamicInterfaceCastableImplementationAttribute]");
            if ((interfaceInfo.Attributes & InterfaceAttributes.ContainsBaseInterfacesWithMembers) != 0)
            {
                writer.WriteLine("""[global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Usage", "CA2256:All members declared in parent interfaces must have an implementation in a DynamicInterfaceCastableImplementation-attributed interface", Justification = "Members in parent interfaces will be implemented through other .NET callable wrappers.")]""");
            }
            writer.WriteLine($"file interface {SymbolFormat.GetDotNetWrapperName(interfaceInfo.Name)} : {interfaceInfo.Name}");
            writer.WriteLine('{');
            writer.Indent++;
        }
        // Slot 0 is for QueryInterface.
        int vtableSlot = 1;
        foreach (var member in interfaceInfo.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (member)
            {
                case MethodInfo methodInfo:
                    EmitMethod(writer, interfaceInfo.Name, methodInfo, vtableSlot++, isComSharpWrapper);
                    break;
                case PropertyInfo propertyInfo:
                    EmitProperty(writer, interfaceInfo.Name, propertyInfo, ref vtableSlot, isComSharpWrapper);
                    break;
                case EventInfo eventInfo:
                    EmitEvent(writer, interfaceInfo.Name, eventInfo, ref vtableSlot, isComSharpWrapper);
                    break;
            }
        }
        writer.Indent--;
        writer.WriteLine(isComSharpWrapper ? "];" : "}");
    }

    private static void EmitMethod(IndentedTextWriter writer, string interfaceName, MethodInfo method, int vtableSlot, bool isComSharpWrapper)
    {
        if (!isComSharpWrapper && method.ReturnType is { RefKind: var kind } && kind != RefKind.None)
        {
            writer.Write($"{GetRefKeyword(kind, forReturn: true)} ");
        }
        var returnType = method.ReturnType?.GetMarshalledParameterType(isComSharpWrapper) ?? "void";
        writer.Write($"{returnType} ");
        if (!isComSharpWrapper)
        {
            writer.Write($"{interfaceName}.{method.Name}");
        }
        EmitSignature(writer, method.Parameters, isComSharpWrapper);
        bool emitMethodBody = method.NeedsMethodBody(isComSharpWrapper);
        if (!emitMethodBody)
        {
            writer.WriteLine(" => ");
        }
        else if (isComSharpWrapper)
        {
            writer.WriteLine(" => {");
        }
        else
        {
            writer.WriteLine();
            writer.WriteLine('{');
        }
        writer.Indent++;
        if (!isComSharpWrapper)
        {
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                if (method.Parameters[i].RefKind == RefKind.Out)
                {
                    // TODO: Use Unsafe.SkipInit if unsafe is enabled?
                    writer.WriteLine($"x{i + 1} = default;");
                }
            }
        }
        var returnContext = MarshalContext.Create(isComSharpWrapper, isReturn: true);
        switch (method.ReturnType)
        {
            case { MarshallingInfo: ParameterMarshallingInfo.Tuple }:
                writer.Write("var ret = ");
                MarshalFunctionCall(writer);
                writer.WriteLine(';');
                writer.Write("return ");
                EmitMarshalCode(writer, method.ReturnType.TypeName, method.ReturnType.MarshallingInfo, returnContext, w => w.Write("ret"));
                writer.WriteLine(';');
                break;
            case var ret:
                if (ret is not null && emitMethodBody)
                {
                    writer.Write("return ");
                }
                EmitMarshalCode(writer, ret?.TypeName ?? "void", ret?.MarshallingInfo ?? ParameterMarshallingInfo.Identity.Instance, returnContext, MarshalFunctionCall);
                if (emitMethodBody)
                {
                    writer.WriteLine(";");
                }
                break;
        }
        writer.Indent--;
        if (!emitMethodBody)
        {
            writer.WriteLine(isComSharpWrapper ? ',' : ';');
        }
        else if (isComSharpWrapper)
        {
            writer.WriteLine("},");
        }
        else
        {
            writer.WriteLine('}');
        }

        void MarshalFunctionCall(IndentedTextWriter writer)
        {
            bool first = true;
            if (isComSharpWrapper)
            {
                writer.Write('(');
                EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                writer.Write($").{method.Name}(");
            }
            else
            {
                EmitGetDelegateCall(writer, interfaceName, method.ReturnType?.TypeName, method.ReturnType?.MarshallingInfo, method.Parameters);
                writer.Write($"({vtableSlot})(");
                EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                first = false;
            }
            int i = 0;
            var paramContext = MarshalContext.Create(isComSharpWrapper);
            Action<IndentedTextWriter> writeParameter = w => w.Write($"x{i + 1}");
            for (; i < method.Parameters.Count; i++)
            {
                if (!first)
                {
                    writer.Write(", ");
                }
                first = false;
                EmitMarshalCode(writer, method.Parameters[i].TypeName, method.Parameters[i].MarshallingInfo, paramContext, writeParameter);
            }
            writer.Write(')');
        }
    }

    private static void EmitProperty(IndentedTextWriter writer, string interfaceName, PropertyInfo property, ref int vtableSlot, bool isComSharpWrapper)
    {
        if (!isComSharpWrapper)
        {
            if (property.Type.RefKind != RefKind.None)
            {
                writer.Write($"{GetRefKeyword(property.Type.RefKind, forReturn: true)} ");
            }
            var propertyName = property.IndexerParameters.Count > 0 ? "this" : property.Name;
            writer.Write($"{property.Type.TypeName} {interfaceName}.{propertyName}");
            if (property.IndexerParameters.Count > 0)
            {
                writer.Write('[');
                for (int i = 0; i < property.IndexerParameters.Count; i++)
                {
                    if (i > 0)
                    {
                        writer.Write(", ");
                    }
                    var parameter = property.IndexerParameters[i];
                    if (parameter.RefKind != RefKind.None)
                    {
                        writer.Write($"{GetRefKeyword(parameter.RefKind)} ");
                    }
                    writer.Write($"{parameter.TypeName} x{i + 1}");
                }
                writer.Write(']');
            }
            writer.WriteLine();
            writer.WriteLine('{');
            writer.Indent++;
        }

        if (property.HasGetter)
        {
            if (isComSharpWrapper)
            {
                writer.Write($"{property.Type.GetMarshalledParameterType(isComSharpWrapper)} ");
                EmitSignature(writer, property.IndexerParameters, isComSharpWrapper);
            }
            else
            {
                writer.Write("get");
            }
            bool emitGetterBody = property.NeedsGetterBody(isComSharpWrapper);
            if (!emitGetterBody)
            {
                writer.WriteLine(" =>");
            }
            else if (isComSharpWrapper)
            {
                writer.WriteLine(" => {");
            }
            else
            {
                writer.WriteLine();
                writer.WriteLine('{');
            }
            writer.Indent++;
            int n = vtableSlot++;
            bool useReturnVariable = property.Type.MarshallingInfo is ParameterMarshallingInfo.Tuple;
            var returnContext = MarshalContext.Create(isComSharpWrapper, isReturn: true);
            if (useReturnVariable)
            {
                writer.Write("var ret = ");
                MarshalGetterCall(writer);
                writer.WriteLine(';');
                writer.Write("return ");
                EmitMarshalCode(writer, property.Type.TypeName, property.Type.MarshallingInfo, returnContext, w => w.Write("ret"));
                writer.WriteLine(';');
            }
            else
            {
                if (emitGetterBody)
                {
                    writer.Write("return ");
                }
                EmitMarshalCode(writer, property.Type.TypeName, property.Type.MarshallingInfo, returnContext, MarshalGetterCall);
                if (emitGetterBody)
                {
                    writer.WriteLine(';');
                }
            }
            writer.Indent--;
            if (!emitGetterBody)
            {
                writer.WriteLine(isComSharpWrapper ? ',' : ';');
            }
            else if (isComSharpWrapper)
            {
                writer.WriteLine("},");
            }
            else
            {
                writer.WriteLine('}');
            }

            void MarshalGetterCall(IndentedTextWriter writer)
            {
                bool first = true;
                if (isComSharpWrapper)
                {
                    writer.Write('(');
                    EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                    writer.Write(')');
                    if (property.IndexerParameters.Count == 0)
                    {
                        writer.Write($".{property.Name}");
                    }
                }
                else
                {
                    EmitGetDelegateCall(writer, interfaceName, property.Type.TypeName, property.Type.MarshallingInfo, property.IndexerParameters);
                    writer.Write($"({n})(");
                    EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                    first = false;
                }
                if (property.IndexerParameters.Count > 0)
                {
                    if (isComSharpWrapper)
                    {
                        writer.Write('[');
                    }
                    var paramContext = MarshalContext.Create(isComSharpWrapper);
                    for (int i = 0; i < property.IndexerParameters.Count; i++)
                    {
                        if (!first)
                        {
                            writer.Write(", ");
                        }
                        first = false;
                        EmitMarshalCode(writer, property.IndexerParameters[i].TypeName, property.IndexerParameters[i].MarshallingInfo, paramContext, w => w.Write($"x{i + 1}"));
                    }
                    if (isComSharpWrapper)
                    {
                        writer.Write(']');
                    }
                }
                if (!isComSharpWrapper)
                {
                    writer.Write(')');
                }
            }
        }

        if (property.HasSetter)
        {
            if (isComSharpWrapper)
            {
                writer.Write("void ");
                EmitSignature(writer, property.IndexerParameters.Concat([property.Type]), isComSharpWrapper);
            }
            else
            {
                writer.Write("set");
            }
            writer.Write(" =>");
            using (writer.EnterIndent())
            {
                bool first = true;
                int n = vtableSlot++;
                if (isComSharpWrapper)
                {
                    writer.Write('(');
                    EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                    writer.Write(')');
                    if (property.IndexerParameters.Count == 0)
                    {
                        writer.Write($".{property.Name}");
                    }
                }
                else
                {
                    EmitGetDelegateCall(writer, interfaceName, null, null, [.. property.IndexerParameters, property.Type]);
                    writer.Write($"({n})(");
                    EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                    first = false;
                }
                var paramContext = MarshalContext.Create(isComSharpWrapper);
                if (property.IndexerParameters.Count > 0)
                {
                    if (isComSharpWrapper)
                    {
                        writer.Write('[');
                    }
                    for (int i = 0; i < property.IndexerParameters.Count; i++)
                    {
                        if (!first)
                        {
                            writer.Write(", ");
                        }
                        first = false;
                        EmitMarshalCode(writer, property.IndexerParameters[i].TypeName, property.IndexerParameters[i].MarshallingInfo, paramContext, w => w.Write($"x{i + 1}"));
                    }
                    if (isComSharpWrapper)
                    {
                        writer.Write(']');
                    }
                }
                writer.Write(isComSharpWrapper ? " = " : ", ");
                EmitMarshalCode(writer, property.Type.TypeName, property.Type.MarshallingInfo, paramContext, w => w.Write(isComSharpWrapper ? $"x{property.IndexerParameters.Count + 1}" : "value"));
                writer.Write(isComSharpWrapper ? "," : ");");
            }
        }

        if (!isComSharpWrapper)
        {
            writer.Indent--;
            writer.WriteLine('}');
        }
    }

    private static void EmitEvent(IndentedTextWriter writer, string interfaceName, EventInfo @event, ref int vtableSlot, bool isComSharpWrapper)
    {
        if (isComSharpWrapper)
        {
            EmitAccessor("+=");
            EmitAccessor("-=");

            void EmitAccessor(string operatorSymbol)
            {
                var paramType = GetMarshalledTypeName(@event.TypeName, @event.MarshallingInfo, isComSharpWrapper);
                writer.Write($"void (object sourceObject, {paramType} x) =>");
                using (writer.EnterIndent())
                {
                    writer.Write('(');
                    EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                    writer.Write(')');
                    writer.Write($".{@event.Name} {operatorSymbol} ");
                    EmitMarshalCode(writer, @event.TypeName, @event.MarshallingInfo, MarshalContext.Create(isComSharpWrapper), w => w.Write("x"));
                    writer.Write(',');
                }
            }
        }
        else
        {
            writer.WriteLine($"event {@event.TypeName} {interfaceName}.{@event.Name}");
            writer.WriteLine('{');
            writer.Indent++;
            EmitAccessor("add", ref vtableSlot);
            EmitAccessor("remove", ref vtableSlot);
            writer.Indent--;
            writer.WriteLine('}');

            void EmitAccessor(string keyword, ref int vtableSlot)
            {
                writer.WriteLine($"{keyword} =>");
                writer.Indent++;
                EmitGetDelegateCall(writer, interfaceName, null, null, [new("value", @event.TypeName, @event.MarshallingInfo)]);
                writer.Write($"({vtableSlot++})(");
                EmitSourceObjectAccessor(writer, interfaceName, isComSharpWrapper);
                writer.WriteLine(", value);");
                writer.Indent--;
            }
        }
    }

    private static void EmitMarshalCode(IndentedTextWriter writer, string typeName, ParameterMarshallingInfo marshallingInfo, MarshalContext context, Action<IndentedTextWriter> writeParameter)
    {
        switch (marshallingInfo)
        {
            case ParameterMarshallingInfo.Identity:
                writeParameter(writer);
                break;
            case ParameterMarshallingInfo.SimpleCast simpleCast:
                if (context.MarshalToComSharp)
                {
                    writer.Write($"({simpleCast.ComSharpType})");
                }
                else
                {
                    writer.Write($"({typeName})");
                }
                writeParameter(writer);
                break;
            case ParameterMarshallingInfo.ComSharpInterface:
                writer.Write(context.IsComSharpWrapper ? "wrappers" : "((IDotNetWrapper)this).Wrappers");
                if (context.MarshalToComSharp)
                {
                    writer.Write($".Marshal(");
                }
                else
                {
                    writer.Write($".Unmarshal<{typeName}>(");
                }
                writeParameter(writer);
                writer.Write(")");
                break;
            case ParameterMarshallingInfo.Tuple tuple:
                writer.Write(tuple.IsValueTuple ? "(" : "new(");
                string elementAccessor = "";
                Action<IndentedTextWriter> writeParameter2 = w => { writeParameter(w); w.Write(elementAccessor); };
                for (int i = 0; i < tuple.ElementTypeNames.Count; i++)
                {
                    if (i > 0)
                    {
                        writer.Write(", ");
                    }
                    elementAccessor = i == 7 && !tuple.IsValueTuple ? ".Rest" : $".Item{i + 1}";
                    EmitMarshalCode(writer, tuple.ElementTypeNames[i], tuple.ElementMarshallingInfos[i], context, writeParameter2);
                }
                writer.Write(')');
                break;
            case ParameterMarshallingInfo.ByReference { Kind: var kind }:
                var keyword = kind switch
                {
                    RefKind.In or RefKind.RefReadOnlyParameter when context.MarshalToComSharp || !context.IsReturn => "in",
                    RefKind.Out when !context.MarshalToComSharp => "out",
                    _ => "ref",
                };
                if (context.MarshalToComSharp)
                {
                    writer.Write($"new({keyword} ");
                    writeParameter(writer);
                    writer.Write(')');
                }
                else
                {
                    writer.Write($"{keyword} ");
                    writeParameter(writer);
                    writer.Write("[0]");
                }
                break;
            case ParameterMarshallingInfo.GenericCollection genericCollection:
                writer.Write($"CollectionMarshaller.Marshal(");
                writeParameter(writer);
                writer.Write(", x => ");
                EmitMarshalCode(writer, genericCollection.ElementTypeName, genericCollection.ElementMarshallingInfo, context, w => w.Write("x"));
                if (!genericCollection.Collection.IsReadOnly)
                {
                    writer.Write(", x => ");
                    EmitMarshalCode(writer, genericCollection.ElementTypeName, genericCollection.ElementMarshallingInfo, context.Invert(), w => w.Write("x"));
                }
                writer.Write(")");
                break;
        }
    }

    private static void EmitSignature(IndentedTextWriter writer, IEnumerable<ParameterInfo> parameters, bool isComSharpWrapper)
    {
        writer.Write('(');
        bool first = true;
        if (isComSharpWrapper)
        {
            writer.Write("object sourceObject");
            first = false;
        }
        int i = 1;
        foreach (var parameter in parameters)
        {
            if (!first)
            {
                writer.Write(", ");
            }
            first = false;
            if (!isComSharpWrapper && parameter.RefKind is var kind && kind != RefKind.None)
            {
                writer.Write($"{GetRefKeyword(kind)} ");
            }
            writer.Write($"{parameter.GetMarshalledParameterType(isComSharpWrapper)} x{i}");
            i++;
        }
        writer.Write(')');
    }

    private static void EmitGetDelegateCall(IndentedTextWriter writer, string interfaceName, string? returnTypeName, ParameterMarshallingInfo? returnMarshallingInfo, IReadOnlyCollection<ParameterInfo> parameters)
    {
        bool hasReturn = returnTypeName != null && returnMarshallingInfo != null;
        writer.Write($"((IDotNetWrapper)this).GetDelegate<{interfaceName}, ");
        writer.Write(hasReturn ? "global::System.Func<object" : "global::System.Action<object");
        foreach (var parameter in parameters)
        {
            writer.Write($", {parameter.GetMarshalledParameterType(marshalToComSharp: true)}");
        }
        if (hasReturn)
        {
            writer.Write($", {returnMarshallingInfo!.GetComSharpType(returnTypeName!)}");
        }
        writer.Write(">>");
    }

    private static void EmitSourceObjectAccessor(IndentedTextWriter writer, string interfaceName, bool isComSharpWrapper)
    {
        if (isComSharpWrapper)
        {
            writer.Write($"({interfaceName})sourceObject");
        }
        else
        {
            writer.Write("((IDotNetWrapper)this).SourceObject");
        }
    }

    private static string GetMarshalledTypeName(string typeName, ParameterMarshallingInfo marshallingInfo, bool marshalToComSharp) =>
        marshalToComSharp ? marshallingInfo.GetComSharpType(typeName) : typeName;

    private static string GetRefKeyword(RefKind refKind, bool forReturn = false)
    {
        return refKind switch
        {
            RefKind.In => forReturn ? "ref readonly" : "in",
            RefKind.Out => "out",
            RefKind.Ref => "ref",
            RefKind.RefReadOnlyParameter => "ref readonly",
            _ => ""
        };
    }

    private readonly struct MarshalContext
    {
        public bool IsComSharpWrapper { get; init; }

        public bool MarshalToComSharp { get; init; }

        public bool IsReturn { get; init; }

        public MarshalContext Invert() => this with { MarshalToComSharp = !MarshalToComSharp };

        public static MarshalContext Create(bool isComSharpWrapper, bool isReturn = false) =>
            new() { IsComSharpWrapper = isComSharpWrapper, MarshalToComSharp = isComSharpWrapper ^ !isReturn, IsReturn = isReturn };
    }
}
