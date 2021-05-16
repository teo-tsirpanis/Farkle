``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.203
  [Host]     : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 5.0.6 (CoreCLR 5.0.621.22011, CoreFX 5.0.621.22011), X64 RyuJIT


```
| Method |       Mean |    Error |   StdDev |    Gen 0 |    Gen 1 |  Gen 2 |  Allocated |
|------- |-----------:|---------:|---------:|---------:|---------:|-------:|-----------:|
| EGTneo |   179.8 μs |  3.59 μs |  4.79 μs |  31.2500 |        - |      - |   96.04 KB |
|  Build | 3,366.1 μs | 56.62 μs | 52.97 μs | 664.0625 | 242.1875 | 3.9063 | 2531.23 KB |
