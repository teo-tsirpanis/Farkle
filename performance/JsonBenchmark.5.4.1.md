``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.200
  [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT


```
|                  Method |     Mean |     Error |    StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------------------ |---------:|----------:|----------:|------:|--------:|---------:|---------:|--------:|-----------:|
|            FarkleCSharp | 5.322 ms | 0.2038 ms | 0.6008 ms |  1.31 |    0.11 | 500.0000 | 250.0000 |       - | 2913.45 KB |
|            FarkleFSharp | 4.577 ms | 0.0571 ms | 0.0534 ms |  1.25 |    0.02 | 500.0000 | 250.0000 |       - | 2913.45 KB |
| FarkleFSharpStaticBlock | 4.219 ms | 0.0683 ms | 0.0638 ms |  1.15 |    0.03 | 500.0000 | 250.0000 |       - | 2909.84 KB |
|       FarkleSyntaxCheck | 3.059 ms | 0.0409 ms | 0.0383 ms |  0.83 |    0.02 | 699.2188 |        - |       - | 2147.16 KB |
|                  Chiron | 3.663 ms | 0.0513 ms | 0.0429 ms |  1.00 |    0.00 | 636.7188 | 296.8750 | 19.5313 | 3644.49 KB |
|               FsLexYacc | 4.183 ms | 0.0761 ms | 0.0712 ms |  1.14 |    0.03 | 257.8125 | 125.0000 |       - | 1558.26 KB |
|                 JsonNET | 1.082 ms | 0.0262 ms | 0.0511 ms |  0.30 |    0.01 | 115.2344 |  56.6406 |       - |  702.46 KB |
