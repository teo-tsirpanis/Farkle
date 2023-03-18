``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT


```
|            Method |      Mean |     Error |    StdDev |    Median | Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------------ |----------:|----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|-----------:|
|      FarkleCSharp | 16.274 ms | 0.3813 ms | 1.1122 ms | 16.020 ms |  2.45 |    0.96 |  781.2500 | 296.8750 | 46.8750 | 4495.58 KB |
|      FarkleFSharp | 16.989 ms | 0.3384 ms | 0.7775 ms | 16.821 ms |  2.58 |    0.83 |  812.5000 | 281.2500 | 31.2500 | 4495.58 KB |
| FarkleSyntaxCheck | 12.010 ms | 0.1616 ms | 0.1350 ms | 11.965 ms |  1.87 |    0.43 | 1203.1250 |        - |       - | 3729.29 KB |
|            Chiron |  6.550 ms | 0.1424 ms | 0.2877 ms |  6.448 ms |  1.00 |    0.25 |  671.8750 | 273.4375 | 93.7500 | 3950.64 KB |
|         FsLexYacc |  4.935 ms | 0.0984 ms | 0.2011 ms |  4.854 ms |  0.75 |    0.19 |  257.8125 | 125.0000 |       - | 1565.95 KB |
|           JsonNET |  1.581 ms | 0.0326 ms | 0.0940 ms |  1.569 ms |  0.24 |    0.00 |  115.2344 |  56.6406 |       - |  710.16 KB |
