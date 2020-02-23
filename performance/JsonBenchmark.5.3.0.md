``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT


```
|                  Method |     Mean |     Error |    StdDev |   Median | Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------------------ |---------:|----------:|----------:|---------:|------:|--------:|----------:|---------:|--------:|-----------:|
|            FarkleCSharp | 6.803 ms | 0.1356 ms | 0.2111 ms | 6.787 ms |  1.59 |    0.17 |  671.8750 | 328.1250 | 23.4375 | 3878.44 KB |
|            FarkleFSharp | 6.615 ms | 0.1321 ms | 0.2448 ms | 6.580 ms |  1.55 |    0.15 |  671.8750 | 328.1250 | 23.4375 | 3878.44 KB |
| FarkleFSharpStaticBlock | 5.884 ms | 0.1214 ms | 0.2909 ms | 5.814 ms |  1.39 |    0.13 |  671.8750 | 328.1250 | 31.2500 | 3874.82 KB |
|       FarkleSyntaxCheck | 4.634 ms | 0.0784 ms | 0.0770 ms | 4.632 ms |  1.15 |    0.05 | 1015.6250 |        - |       - | 3112.15 KB |
|                  Chiron | 4.276 ms | 0.1434 ms | 0.4137 ms | 4.135 ms |  1.00 |    0.00 |  636.7188 | 296.8750 | 23.4375 | 3644.49 KB |
|               FsLexYacc | 4.606 ms | 0.0894 ms | 0.1282 ms | 4.600 ms |  1.10 |    0.11 |  257.8125 | 125.0000 |       - | 1558.25 KB |
|                 JsonNET | 1.149 ms | 0.0229 ms | 0.0214 ms | 1.139 ms |  0.28 |    0.02 |  113.2813 |  56.6406 |       - |  702.47 KB |
