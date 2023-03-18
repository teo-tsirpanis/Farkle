``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.1.701
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|            Method | DynamicallyReadInput |      Mean |     Error |    StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 | Allocated |
|------------------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|----------:|
|      **FarkleCSharp** |                **False** | **13.030 ms** | **0.1150 ms** | **0.1019 ms** |  **0.95** |    **0.02** |  **687.5000** | **312.5000** |       **-** |   **3.85 MB** |
|      FarkleFSharp |                False | 13.673 ms | 0.2008 ms | 0.1878 ms |  1.00 |    0.00 |  687.5000 | 312.5000 |       - |   3.85 MB |
| FarkleSyntaxCheck |                False |  9.758 ms | 0.1310 ms | 0.1225 ms |  0.71 |    0.01 | 1015.6250 |        - |       - |   3.09 MB |
|            Chiron |                False |  6.634 ms | 0.0461 ms | 0.0432 ms |  0.49 |    0.01 |  617.1875 | 296.8750 |  7.8125 |   3.57 MB |
|                   |                      |           |           |           |       |         |           |          |         |           |
|      **FarkleCSharp** |                 **True** | **13.898 ms** | **0.1847 ms** | **0.1727 ms** |  **0.95** |    **0.01** |  **671.8750** | **328.1250** |       **-** |   **3.86 MB** |
|      FarkleFSharp |                 True | 14.626 ms | 0.1253 ms | 0.1172 ms |  1.00 |    0.00 |  671.8750 | 328.1250 |       - |   3.86 MB |
| FarkleSyntaxCheck |                 True | 10.729 ms | 0.1103 ms | 0.1032 ms |  0.73 |    0.01 | 1031.2500 |        - |       - |    3.1 MB |
|            Chiron |                 True |  7.408 ms | 0.0638 ms | 0.0566 ms |  0.51 |    0.01 |  664.0625 | 343.7500 | 93.7500 |   3.87 MB |
