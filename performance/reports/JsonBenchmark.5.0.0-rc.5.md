``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.14393.2457 (1607/AnniversaryUpdate/Redstone1), VM=Hyper-V
Intel Xeon CPU E5-2697 v3 2.60GHz, 1 CPU, 2 logical and 2 physical cores
.NET Core SDK=2.2.203
  [Host]     : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT


```
|            Method | DynamicallyReadInput |      Mean |     Error |    StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 | Allocated |
|------------------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|----------:|
|      **FarkleCSharp** |                **False** | **24.182 ms** | **0.4781 ms** | **0.6702 ms** |  **0.97** |    **0.04** | **1375.0000** |  **31.2500** |       **-** |   **8.62 MB** |
|      FarkleFSharp |                False | 24.918 ms | 0.4494 ms | 0.6727 ms |  1.00 |    0.00 | 1250.0000 |  31.2500 |       - |   8.02 MB |
| FarkleSyntaxCheck |                False | 20.098 ms | 0.4023 ms | 0.9404 ms |  0.82 |    0.04 | 1156.2500 |        - |       - |   7.33 MB |
|            Chiron |                False |  8.863 ms | 0.1830 ms | 0.4016 ms |  0.36 |    0.02 |  562.5000 | 281.2500 |       - |   3.57 MB |
|                   |                      |           |           |           |       |         |           |          |         |           |
|      **FarkleCSharp** |                 **True** | **24.184 ms** | **0.4631 ms** | **0.3867 ms** |  **0.97** |    **0.02** | **1375.0000** |  **31.2500** |       **-** |   **8.63 MB** |
|      FarkleFSharp |                 True | 24.900 ms | 0.3676 ms | 0.2870 ms |  1.00 |    0.00 | 1250.0000 |  31.2500 |       - |   8.03 MB |
| FarkleSyntaxCheck |                 True | 19.851 ms | 0.3939 ms | 0.6899 ms |  0.80 |    0.03 | 1156.2500 |  31.2500 |       - |   7.34 MB |
|            Chiron |                 True |  9.053 ms | 0.1808 ms | 0.3306 ms |  0.37 |    0.02 |  562.5000 | 296.8750 | 78.1250 |   3.86 MB |
