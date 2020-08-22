``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.423 (2004/?/20H1)
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.302
  [Host]     : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT


```
|           Method |       Mean |    Error |   StdDev |    Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|----------------- |-----------:|---------:|---------:|---------:|---------:|------:|-----------:|
|        Base64EGT |   441.8 μs |  3.94 μs |  3.49 μs |  58.5938 |   3.9063 |     - |   183.2 KB |
|     Base64EGTneo |   250.2 μs |  3.81 μs |  2.98 μs |  33.2031 |   0.4883 |     - |  102.57 KB |
|            Build | 4,234.4 μs | 80.00 μs | 85.60 μs | 671.8750 | 250.0000 |     - | 3103.15 KB |
| BuildPrecompiled |   622.5 μs |  9.45 μs |  8.38 μs |  57.6172 |  18.5547 |     - |   223.2 KB |
