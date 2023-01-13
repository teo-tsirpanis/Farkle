# `Farkle.Tools.VisualStudioWorkaround`

This package allows users of Visual Studio for Windows[^msbuild] to target any .NET version in their apps that use [Farkle's precompiler](https://teo-tsirpanis.github.io/Farkle/the-precompiler.html).

When not using the .NET SDK, the precompiler runs in an external process (the _precompiler worker_) that targets the latest LTS version at the time of its release. This means for example that until .NET 8 gets released and Farkle catches up, building a precompiler-enabled .NET 7 app in Visual Studio is not possible.

This package injects a couple of MSBuild targets in the build process that set the `DOTNET_ROLL_FORWARD` environment variable to `LatestMajor` for the duration of the precompiler worker's invocation, making it run on the latest available version of .NET. If the package is installed in an app that gets built with the .NET SDK it will do nothing.

A real fix of the problem will be available in Farkle 7, making this package unnecessary. It will advise users to uninstall it, if used together with Farkle 7.

[^msbuild]: or those that use the .NET Framework-based `msbuild` command to build their apps
