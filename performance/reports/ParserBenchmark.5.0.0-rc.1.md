``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.301
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|                      Method | DynamicallyReadInput |     Mean |     Error |    StdDev |    Gen 0 |   Gen 1 | Gen 2 | Allocated |
|---------------------------- |--------------------- |---------:|----------:|----------:|---------:|--------:|------:|----------:|
|         **GOLDMetaLanguageAST** |                **False** | **3.373 ms** | **0.0314 ms** | **0.0294 ms** | **453.1250** | **82.0313** |     **-** |   **1.52 MB** |
| GOLDMetaLanguageSyntaxCheck |                False | 2.933 ms | 0.0259 ms | 0.0243 ms | 441.4063 |       - |     - |   1.34 MB |
|         **GOLDMetaLanguageAST** |                 **True** | **3.490 ms** | **0.0299 ms** | **0.0280 ms** | **460.9375** | **70.3125** |     **-** |   **1.52 MB** |
| GOLDMetaLanguageSyntaxCheck |                 True | 3.024 ms | 0.0229 ms | 0.0214 ms | 445.3125 |       - |     - |   1.34 MB |
