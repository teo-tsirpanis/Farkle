# FARKLE0018: Precompiler not supported in this build environment

This error is emitted when the precompiler runs on an unsupported build environment. The following build environments are supported:

* Any .NET SDK version [currently in support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
  * This includes any IDE that uses the `dotnet` commands for building, such as Rider, and the C# Dev Kit.
* Visual Studio 2026 or later.

To fix this error, ensure that you are using a supported .NET SDK version and IDE.
