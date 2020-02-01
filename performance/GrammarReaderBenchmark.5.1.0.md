``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT


```
|       Method |        Mean |     Error |      StdDev |     Gen 0 |  Gen 1 | Gen 2 |  Allocated |
|------------- |------------:|----------:|------------:|----------:|-------:|------:|-----------:|
|    Base64EGT |    397.3 us |   5.76 us |     4.50 us |   55.6641 | 1.9531 |     - |   171.1 KB |
| BuildGrammar | 13,816.3 us | 710.18 us | 2,037.63 us | 2000.0000 |      - |     - | 7446.34 KB |
