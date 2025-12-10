# FARKLE0015: Exception when invoking precompiler input method

This error is emitted when an exception is thrown while the precompiler invokes a method marked with @"Farkle.Builder.PrecompilerInputAttribute". The exception's message and stack trace are included, to help diagnose the issue.

In order to fix it, make sure that precompiler input methods do not throw exceptions when invoked.

If the escepption was of type @"System.TypeLoadException", the precompiler failed to resolve one of your project's dependencies. This can happen because the precompiler runs in the same .NET runtime as the .NET SDK in use, and some native or platform-specific dependencies might not be available in that context. In that case, make sure that methods marked with @"Farkle.Builder.PrecompilerInputAttribute" have the minimum amount of dependencies needed to construct the grammar to precompile.
