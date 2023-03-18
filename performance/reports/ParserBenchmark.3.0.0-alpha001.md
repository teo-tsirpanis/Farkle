``` ini

BenchmarkDotNet=v0.11.0, OS=Windows 10.0.17134.165 (1803/April2018Update/Redstone4)
AMD Ryzen 5 1600 Six-Core Processor (Max: 3.20GHz), 1 CPU, 12 logical and 6 physical cores
.NET Core SDK=2.1.302
  [Host]     : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT


```
|                    Method |      Mean |    Error |   StdDev | Scaled | ScaledSD |      Gen 0 |     Gen 1 |   Allocated |
|-------------------------- |----------:|---------:|---------:|-------:|---------:|-----------:|----------:|------------:|
|    GOLDMetaLanguageFarkle | 146.11 ms | 2.268 ms | 2.122 ms |   1.65 |     0.04 | 47000.0000 | 2000.0000 | 57482.72 KB |
|   GOLDMetaLanguageLazarus |  88.76 ms | 1.927 ms | 1.803 ms |   1.00 |     0.00 |          - |         - |     1.33 KB |
