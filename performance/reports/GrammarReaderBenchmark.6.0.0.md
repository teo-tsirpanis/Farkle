``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.103
  [Host]     : .NET Core 5.0.3 (CoreCLR 5.0.321.7212, CoreFX 5.0.321.7212), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 5.0.3 (CoreCLR 5.0.321.7212, CoreFX 5.0.321.7212), X64 RyuJIT


```
| Method |       Mean |    Error |   StdDev |    Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|------- |-----------:|---------:|---------:|---------:|---------:|------:|-----------:|
| EGTneo |   179.0 μs |  0.89 μs |  0.79 μs |  31.4941 |        - |     - |   96.81 KB |
|  Build | 3,635.2 μs | 47.15 μs | 44.11 μs | 804.6875 | 261.7188 |     - | 2829.93 KB |
