``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT


```
|       Method |       Mean |     Error |    StdDev |     Median |     Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------- |-----------:|----------:|----------:|-----------:|----------:|---------:|--------:|-----------:|
|    Base64EGT |   415.3 us |   9.24 us |  22.32 us |   406.7 us |   54.6875 |  10.7422 |       - |  175.45 KB |
| BuildGrammar | 9,317.5 us | 185.34 us | 510.49 us | 9,219.3 us | 2000.0000 | 430.0000 | 10.0000 | 6735.77 KB |
