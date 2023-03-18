``` ini

BenchmarkDotNet=v0.11.0, OS=Windows 10.0.17134.165 (1803/April2018Update/Redstone4)
AMD Ryzen 5 1600 Six-Core Processor (Max: 3.20GHz), 1 CPU, 12 logical and 6 physical cores
.NET Core SDK=2.1.302
  [Host]     : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT


```
|                        Method |      Mean |    Error |   StdDev | Scaled | ScaledSD |      Gen 0 |     Gen 1 |   Allocated |
|------------------------------ |----------:|---------:|---------:|-------:|---------:|-----------:|----------:|------------:|
|   GOLDMetaLanguageFarkleEager | 197.72 ms | 2.526 ms | 2.239 ms |   2.22 |     0.05 | 70000.0000 | 2000.0000 | 80080.37 KB |
|    GOLDMetaLanguageFarkleLazy | 204.15 ms | 4.073 ms | 3.810 ms |   2.29 |     0.06 | 73000.0000 | 2000.0000 | 83619.43 KB |
|       GOLDMetaLanguageLazarus |  89.25 ms | 1.747 ms | 1.634 ms |   1.00 |     0.00 |          - |         - |      1.3 KB |
