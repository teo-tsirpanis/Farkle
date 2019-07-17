``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.301
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|                      Method | DynamicallyReadInput |     Mean |     Error |    StdDev |   Median |    Gen 0 |   Gen 1 | Gen 2 | Allocated |
|---------------------------- |--------------------- |---------:|----------:|----------:|---------:|---------:|--------:|------:|----------:|
|         **GOLDMetaLanguageAST** |                **False** | **3.063 ms** | **0.0612 ms** | **0.0680 ms** | **3.037 ms** | **449.2188** | **93.7500** |     **-** |   **1.51 MB** |
| GOLDMetaLanguageSyntaxCheck |                False | 2.719 ms | 0.0528 ms | 0.0853 ms | 2.736 ms | 441.4063 |       - |     - |   1.33 MB |
|         **GOLDMetaLanguageAST** |                 **True** | **3.669 ms** | **0.0728 ms** | **0.1943 ms** | **3.607 ms** | **460.9375** | **70.3125** |     **-** |   **1.52 MB** |
| GOLDMetaLanguageSyntaxCheck |                 True | 3.374 ms | 0.0954 ms | 0.2812 ms | 3.341 ms | 445.3125 |       - |     - |   1.34 MB |
