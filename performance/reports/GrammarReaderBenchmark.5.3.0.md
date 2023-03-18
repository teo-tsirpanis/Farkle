``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT


```
|       Method |       Mean |     Error |   StdDev |     Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------- |-----------:|----------:|---------:|----------:|---------:|--------:|-----------:|
|    Base64EGT |   425.0 us |   5.89 us |  5.51 us |   55.6641 |   6.8359 |       - |  175.45 KB |
| BuildGrammar | 8,696.6 us | 102.98 us | 85.99 us | 2020.0000 | 430.0000 | 20.0000 | 6735.77 KB |
