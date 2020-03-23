``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.200
  [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT


```
|       Method |       Mean |     Error |    StdDev |     Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|------------- |-----------:|----------:|----------:|----------:|---------:|------:|-----------:|
|    Base64EGT |   406.6 us |   2.59 us |   2.42 us |   56.6406 |   1.4648 |     - |  175.45 KB |
| BuildGrammar | 8,585.1 us | 177.81 us | 157.63 us | 2090.0000 | 490.0000 |     - | 6986.51 KB |
