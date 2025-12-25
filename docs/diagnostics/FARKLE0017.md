# FARKLE0017: Failed to unload input assembly after precompilation

This warning is emitted when the precompiler fails to unload your project's assembly after it finishes discovering and building its grammars. While this will not prevent the precompiler from completing successfully, it may lead to memory leaks in the reusable MSBuild processes, and likely indicates a bug in your code.

To fix this warning, ensure that your methods marked with @"Farkle.Builder.PrecompilerInputAttribute" do the minimum necessary work to construct the grammars to precompile. If the warning persists, consult the [.NET documentation](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability) to help you with troubleshooting, or open an issue on the [Farkle GitHub repository](https://github.com/teo-tsirpanis/Farkle/issues).
