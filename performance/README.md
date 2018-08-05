This folder contains the performance benchmark results of the library.

Some exploratory benchmarks that do not belong in the namespace `Farkle.Benchmarks.*` are not included here because they have do not measure the performance of the library itself.

For older versions than 3.0.0, I hastily backported the benchmarks to .NET Core 2.1 and BenchmarkDotNet 0.11, to make the comparison more equal. And there were no benchmarks for version 1.0.0.