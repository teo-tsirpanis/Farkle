# COM# specification

Version 1.0

## Introduction

COM# is an object-oriented [application binary interface][abi] built on top of the .NET type system, that allows code in separate .NET assemblies to interoperate. Usually, this is done by defining interfaces in a shared assembly, and having both sides reference that assembly. COM# however does not require a shared assembly, and works by exchanging types in the BCL.

True to its name, COM# is inspired by [COM]. However, it is significantly easier to implement and use, since it can use most affordances of .NET managed code, eliminating nuances such as reference counting and memory management, and increasing the amount of supported parameter and return types.

> [!IMPORTANT]
> At this moment, COM# is used as an implementation detail of the [Farkle precompiler](precompiler.md), and is not intended for general use. Not all aspects of the specification are used in practice, and may change in the future.

## Definitions

### Interface

A COM# interface is a contract that defines a set of methods that can be called on an object. Like COM, it is uniquely identified by an interface ID (IID), which is a GUID. This is an example of a COM# interface:

```csharp
[Guid("1AAFEF59-3840-47A3-8F38-45747927EB23")]
public interface ICalculator
{
    int Add(int a, int b);
    int Subtract(int a, int b);
}
```

COM# interfaces cannot be generic.

### Object

Each assembly has its own definition of a COM# interface, which means that two assemblies cannot directly exchange objects implementing COM# interfaces. Instead, they have to convert them to a COM# object.

A COM# object is an interoperable representation of an object that implements one or more COM# interfaces. It is defined as a `ValueTuple<object, Delegate[]>`. The first member of the tuple is called the _source object_, and the second member is a [virtual method table][vtable], called the _vtable_.

The source object is what gives the COM# object its identity; two COM# objects are equal if and only if their source objects are equal.

The vtable is an array of delegates that allow calling the methods defined in a COM# interface. The following sections detail how the vtable is constructed.

The first delegate of a vtable is called the _query interface function_, and is always a `Func<object, Guid, Delegate[]?>` which is used to query whether the source object implements a COM# interface with a given IID. If the object implements the requested interface, the delegate returns a vtable for that interface; otherwise, it returns `null`. The subsequent delegates correspond to the interface methods, in the order they are declared in the interface.

Vtables are immutable and must not be modified after creation. A vtable does not signify which interface it implements, but this is not a problem, because consumers of any COM# object can use the query interface function to obtain the vtable for any interface they want.

### Shared assemblies

As mentioned before, two assemblies that exchange COM# objects are not supposed to be directly referenced. There are however some assemblies that both reference, known as _shared assemblies_. The set of shared assemblies consists of at least the "system" assemblies that are required to implement the specification, but may also include other assemblies that have types that will be exchanged in a COM# interface.

### Inheritance

Unlike COM, COM# does not support direct inheritance between COM# interfaces. Instead, like WinRT, an interface may implicitly require that its implementations also implement other interfaces. This may be expressed as interface inheritance in COM# wrappers.

A COM# interface can inherit from an interface that is declared in a shared assembly — closed under generic specializations, arrays, and function pointers. This interface can be accessed by casting the source object to that interface type.

## Vtable construction

Each member of a COM# interface corresponds to one or more entries in its vtable. As mentioned before, the first entry is always the query interface function. After that, each member is represented as follows:

* Methods that return `void` correspond to an `Action<object, ...>` delegate, where the first parameter of the delegate is the target object, and each subsequent delegate parameter corresponds to the [marshalled type](#type-marshalling) of the respective method parameter.
* Methods that return a value correspond to a `Func<object, ..., TResult>` delegate, where the first parameter of the delegate is the target object, and each subsequent delegate parameter corresponds to the [marshalled type](#type-marshalling) of the respective method parameter, and `TResult` corresponds to the [marshalled type](#type-marshalling) of the method's return type.
* Properties correspond to a series of methods: the getter method, and then the setter method, if each exists. The above rules for methods apply recursively.
* Events correspond to a series of methods: the `add` method, and then the `remove` method, if each exists. The above rules for methods apply recursively.

## Type marshalling

The following types are supported as parameters and return types in COM# interfaces. In some cases, the type gets marshalled to another type when passed through the vtable delegate.

* Types that are declared in a shared assembly — closed under generic specializations, arrays, and function pointers — are marshalled unchanged.
* COM# interfaces are marshalled as COM# objects, with a vtable that corresponds to the interface type.
* Enums that are not declared in a shared assembly are marshalled as their underlying type.
* Unmanaged pointers of any type are marshalled as `IntPtr`.
* By-reference parameters of a type declared in a shared assembly — closed under generic specializations, arrays, and function pointers — are marshalled as spans of that type, that contain the reference. If the parameter is an `in` parameter, it is marshalled as `ReadOnlySpan<T>`, otherwise as `Span<T>`.
* Tuples (both reference and value tuples) are marshalled as tuples of the same kind and arity, with each element being [marshalled](#type-marshalling) recursively.
* Generic collection interfaces (`IEnumerable<T>`, `IEnumerator<T>`, `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`) where `T` does not belong to a shared assembly, are marshalled as the respective collection interface, containing values of the [marshalled type](#type-marshalling) of `T`. Each method of the collection interface will have its parameters and return types [marshalled](#type-marshalling) recursively.

Marshalling any other type is not supported.

> [!NOTE]
> Due to runtime limitations, it is possible that even with the above rules, some interfaces cannot be represented in COM#. For example, methods with more than 15 parameters, or methods that accept or return `ref struct`s prior to .NET 9.

## Interface compatibility

Changes to a COM# interface after publication must maintain binary compatibility. Binary-incompatible changes include but are not limited to:

* Making any change to the interface that results in a different vtable representation, e.g. adding, removing, reordering members, or changing the signature of a member. Renaming a member or parameter is allowed, as well as changing a parameter in a way that marshalls to the same type as before.
* Updating a member to accept or return a COM# interface with a different IID.

In such cases, the COM# interface is considered a different interface, and as such its IID must be changed.

## Specification versioning

The specification's version is stated at the beginning of this document, and will change under the following circumstances:

* Additive non-breaking changes do not require a version increase.
* Breaking changes to the marshalling rules require a minor version increase.
* Other breaking changes require a major version increase.

A COM# interface does not signify which version of the specification it adheres to. Assemblies will have to know it in advance, in an implementation-defined manner.

A COM# interface cannot require implementing an interface defined under a different major version of the specification.

## Future directions

* __Using function pointers in vtables.__ Using function pointers instead of delegates in vtables was considered — and would have simplified some marshalling rules, but was rejected for now, mainly because F# does not support function pointers, and also because of the reduced safety and the corruption risks if COM# interface definitions between assemblies diverge. This might be reconsidered in the future.
* __Activation.__ The COM# equivalent of COM activation would be a standardized way to create COM# objects from a given `System.Reflection.Assembly` and a GUID representing the class (CLSID), by calling a static method in a predefined place in the assembly. This is not currently specified. Each implementation will have to specify its own COM# "entry point" for an assembly, that will have to be called with reflection.

[abi]: https://en.wikipedia.org/wiki/Application_binary_interface
[com]: https://en.wikipedia.org/wiki/Component_Object_Model
[vtable]: https://en.wikipedia.org/wiki/Virtual_method_table
