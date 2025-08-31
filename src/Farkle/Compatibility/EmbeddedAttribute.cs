namespace Microsoft.CodeAnalysis;

/// <summary>
/// Instructs compilers to treat a type as visible only within the assembly it was compiled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Delegate)]
internal sealed class EmbeddedAttribute : Attribute;
