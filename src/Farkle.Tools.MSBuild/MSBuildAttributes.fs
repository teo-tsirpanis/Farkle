namespace Microsoft.Build.Framework

open System

[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)>]
type MSBuildMultiThreadableTaskAttribute() = inherit Attribute()
