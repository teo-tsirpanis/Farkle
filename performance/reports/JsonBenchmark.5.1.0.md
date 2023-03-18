``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT


```
|            Method |      Mean |     Error |    StdDev |    Median | Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 | Allocated |
|------------------ |----------:|----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|----------:|
|      FarkleCSharp | 15.671 ms | 0.3349 ms | 0.9280 ms | 15.228 ms |  1.12 |    0.07 |  843.7500 | 281.2500 |       - |   4.51 MB |
|      FarkleFSharp | 14.958 ms | 0.0710 ms | 0.0664 ms | 14.949 ms |  1.00 |    0.00 |  812.5000 | 328.1250 | 15.6250 |   4.51 MB |
| FarkleSyntaxCheck | 11.534 ms | 0.0707 ms | 0.0661 ms | 11.516 ms |  0.77 |    0.00 | 1250.0000 |        - |       - |   3.77 MB |
|            Chiron |  6.394 ms | 0.0716 ms | 0.0634 ms |  6.392 ms |  0.43 |    0.00 |  671.8750 | 218.7500 | 93.7500 |   3.86 MB |
