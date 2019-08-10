``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.1.701
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|                      Method | DynamicallyReadInput |     Mean |     Error |    StdDev |    Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|---------------------------- |--------------------- |---------:|----------:|----------:|---------:|---------:|------:|-----------:|
|         **GOLDMetaLanguageAST** |                **False** | **2.037 ms** | **0.0386 ms** | **0.0379 ms** | **347.6563** |  **85.9375** |     **-** | **1275.76 KB** |
| GOLDMetaLanguageSyntaxCheck |                False | 1.585 ms | 0.0121 ms | 0.0113 ms | 304.6875 |        - |     - |     940 KB |
|         **GOLDMetaLanguageAST** |                 **True** | **2.500 ms** | **0.0291 ms** | **0.0272 ms** | **363.2813** | **109.3750** |     **-** | **1284.01 KB** |
| GOLDMetaLanguageSyntaxCheck |                 True | 1.941 ms | 0.0085 ms | 0.0075 ms | 308.5938 |        - |     - |  948.25 KB |
