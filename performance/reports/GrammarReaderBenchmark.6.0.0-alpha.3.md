``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.100
  [Host]     : .NET Core 5.0.0 (CoreCLR 5.0.20.51904, CoreFX 5.0.20.51904), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 5.0.0 (CoreCLR 5.0.20.51904, CoreFX 5.0.20.51904), X64 RyuJIT


```
| Method |       Mean |    Error |   StdDev |    Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|------- |-----------:|---------:|---------:|---------:|---------:|------:|-----------:|
| EGTneo |   176.5 μs |  2.23 μs |  1.86 μs |  31.4941 |   0.2441 |     - |   97.21 KB |
|  Build | 3,791.0 μs | 73.33 μs | 81.51 μs | 695.3125 | 261.7188 |     - | 3104.76 KB |
