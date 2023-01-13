# Farkle.Tools.VisualStudioWorkaround

This package allows users of Visual Studio for Windows\* to target any .NET version in their apps that use [Farkle's precompiler](https://teo-tsirpanis.github.io/Farkle/the-precompiler.html). If the package is installed in an app that gets built with the .NET SDK it will do nothing.

A real fix of the problem will be available in Farkle 7, making this package unnecessary. It will advise users to uninstall it, if used together with Farkle 7 or greater.

\* and those that use the .NET Framework-based `msbuild` command to build their apps
