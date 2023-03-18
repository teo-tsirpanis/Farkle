``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.1.701
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|            Method | DynamicallyReadInput |      Mean |     Error |    StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 | Allocated |
|------------------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|----------:|
|      **FarkleCSharp** |                **False** | **12.840 ms** | **0.1486 ms** | **0.1317 ms** |  **0.96** |    **0.02** |  **687.5000** | **312.5000** | **15.6250** |   **3.85 MB** |
|      FarkleFSharp |                False | 13.439 ms | 0.1813 ms | 0.1696 ms |  1.00 |    0.00 |  687.5000 | 312.5000 | 31.2500 |   3.85 MB |
| FarkleSyntaxCheck |                False |  9.481 ms | 0.1385 ms | 0.1296 ms |  0.71 |    0.01 | 1015.6250 |        - |       - |   3.09 MB |
|            Chiron |                False |  6.447 ms | 0.0290 ms | 0.0271 ms |  0.48 |    0.01 |  617.1875 | 296.8750 |  7.8125 |   3.57 MB |
|                   |                      |           |           |           |       |         |           |          |         |           |
|      **FarkleCSharp** |                 **True** | **13.574 ms** | **0.0643 ms** | **0.0570 ms** |  **0.91** |    **0.01** |  **671.8750** | **312.5000** |       **-** |   **3.86 MB** |
|      FarkleFSharp |                 True | 14.849 ms | 0.1182 ms | 0.1048 ms |  1.00 |    0.00 |  671.8750 | 312.5000 |       - |   3.86 MB |
| FarkleSyntaxCheck |                 True | 10.319 ms | 0.0431 ms | 0.0404 ms |  0.69 |    0.01 | 1031.2500 |        - |       - |    3.1 MB |
|            Chiron |                 True |  7.026 ms | 0.0238 ms | 0.0211 ms |  0.47 |    0.00 |  664.0625 | 343.7500 | 93.7500 |   3.87 MB |
