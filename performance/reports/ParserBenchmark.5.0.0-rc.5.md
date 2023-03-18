``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.14393.2457 (1607/AnniversaryUpdate/Redstone1), VM=Hyper-V
Intel Xeon CPU E5-2697 v3 2.60GHz, 1 CPU, 2 logical and 2 physical cores
.NET Core SDK=2.2.203
  [Host]     : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT


```
|                      Method | DynamicallyReadInput |     Mean |     Error |    StdDev |    Gen 0 |   Gen 1 | Gen 2 | Allocated |
|---------------------------- |--------------------- |---------:|----------:|----------:|---------:|--------:|------:|----------:|
|         **GOLDMetaLanguageAST** |                **False** | **3.677 ms** | **0.0674 ms** | **0.0563 ms** | **242.1875** | **74.2188** |     **-** |   **1.51 MB** |
| GOLDMetaLanguageSyntaxCheck |                False | 3.439 ms | 0.0681 ms | 0.0977 ms | 210.9375 |       - |     - |   1.33 MB |
|         **GOLDMetaLanguageAST** |                 **True** | **3.806 ms** | **0.0750 ms** | **0.1001 ms** | **242.1875** | **74.2188** |     **-** |   **1.52 MB** |
| GOLDMetaLanguageSyntaxCheck |                 True | 3.341 ms | 0.0394 ms | 0.0349 ms | 210.9375 |  3.9063 |     - |   1.33 MB |
