``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.1.701
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|                      Method | DynamicallyReadInput |     Mean |     Error |    StdDev |    Gen 0 |    Gen 1 | Gen 2 |  Allocated |
|---------------------------- |--------------------- |---------:|----------:|----------:|---------:|---------:|------:|-----------:|
|         **GOLDMetaLanguageAST** |                **False** | **2.089 ms** | **0.0280 ms** | **0.0262 ms** | **355.4688** |  **93.7500** |     **-** | **1275.82 KB** |
| GOLDMetaLanguageSyntaxCheck |                False | 1.715 ms | 0.0341 ms | 0.0579 ms | 304.6875 |        - |     - |  940.06 KB |
|         **GOLDMetaLanguageAST** |                 **True** | **2.699 ms** | **0.0331 ms** | **0.0310 ms** | **355.4688** | **101.5625** |     **-** | **1284.07 KB** |
| GOLDMetaLanguageSyntaxCheck |                 True | 2.041 ms | 0.0251 ms | 0.0210 ms | 308.5938 |        - |     - |  948.31 KB |
