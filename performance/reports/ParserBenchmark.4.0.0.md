``` ini

BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17763.253 (1809/October2018Update/Redstone5)
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.102
  [Host]     : .NET Core 2.1.6 (CoreCLR 4.6.27019.06, CoreFX 4.6.27019.05), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.6 (CoreCLR 4.6.27019.06, CoreFX 4.6.27019.05), 64bit RyuJIT


```
|                      Method | BufferSize |     Mean |     Error |    StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
|---------------------------- |----------- |---------:|----------:|----------:|------------:|------------:|------------:|--------------------:|
|         **GOLDMetaLanguageAST** |          **0** | **5.028 ms** | **0.0135 ms** | **0.0113 ms** |   **1867.1875** |    **242.1875** |           **-** |             **5.85 MB** |
| GOLDMetaLanguageSyntaxCheck |          0 | 4.107 ms | 0.0301 ms | 0.0282 ms |   1882.8125 |           - |           - |             5.65 MB |
|         **GOLDMetaLanguageAST** |        **256** | **5.333 ms** | **0.0548 ms** | **0.0513 ms** |   **1859.3750** |    **273.4375** |           **-** |             **5.85 MB** |
| GOLDMetaLanguageSyntaxCheck |        256 | 4.216 ms | 0.0346 ms | 0.0324 ms |   1882.8125 |           - |           - |             5.66 MB |
|         **GOLDMetaLanguageAST** |        **512** | **5.368 ms** | **0.0256 ms** | **0.0239 ms** |   **1859.3750** |    **273.4375** |           **-** |             **5.85 MB** |
| GOLDMetaLanguageSyntaxCheck |        512 | 4.235 ms | 0.0239 ms | 0.0224 ms |   1882.8125 |           - |           - |             5.66 MB |
|         **GOLDMetaLanguageAST** |       **1144** | **5.380 ms** | **0.0260 ms** | **0.0243 ms** |   **1859.3750** |    **273.4375** |           **-** |             **5.85 MB** |
| GOLDMetaLanguageSyntaxCheck |       1144 | 4.229 ms | 0.0205 ms | 0.0192 ms |   1882.8125 |           - |           - |             5.66 MB |
|         **GOLDMetaLanguageAST** |       **2048** | **5.381 ms** | **0.0351 ms** | **0.0329 ms** |   **1859.3750** |    **273.4375** |           **-** |             **5.85 MB** |
| GOLDMetaLanguageSyntaxCheck |       2048 | 4.187 ms | 0.0226 ms | 0.0200 ms |   1882.8125 |           - |           - |             5.66 MB |
