``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.423 (2004/?/20H1)
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.302
  [Host]     : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT


```
|         Method |       Mean |    Error |   StdDev | Ratio | Rank |    Gen 0 |    Gen 1 |   Gen 2 | Allocated |
|--------------- |-----------:|---------:|---------:|------:|-----:|---------:|---------:|--------:|----------:|
|   FarkleCSharp | 3,436.6 μs | 34.27 μs | 30.38 μs |  0.90 |    3 | 500.0000 | 250.0000 |       - | 2983416 B |
|   FarkleFSharp | 3,470.1 μs | 30.89 μs | 27.38 μs |  0.90 |    3 | 500.0000 | 250.0000 |       - | 2983416 B |
|         Chiron | 3,833.5 μs | 30.08 μs | 28.13 μs |  1.00 |    4 | 628.9063 | 308.5938 | 15.6250 | 3732008 B |
|      FsLexYacc | 4,177.6 μs | 37.84 μs | 35.40 μs |  1.09 |    5 | 257.8125 | 125.0000 |       - | 1595648 B |
|        JsonNET | 1,083.5 μs |  7.93 μs |  7.41 μs |  0.28 |    2 | 115.2344 |  56.6406 |       - |  719327 B |
| SystemTextJson |   281.5 μs |  2.37 μs |  2.21 μs |  0.07 |    1 |        - |        - |       - |      81 B |
