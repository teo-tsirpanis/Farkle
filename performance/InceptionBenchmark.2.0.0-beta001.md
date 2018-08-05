``` ini

BenchmarkDotNet=v0.11.0, OS=Windows 10.0.17134.165 (1803/April2018Update/Redstone4)
AMD Ryzen 5 1600 Six-Core Processor (Max: 3.20GHz), 1 CPU, 12 logical and 6 physical cores
.NET Core SDK=2.1.302
  [Host]     : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT


```
|                    Method |      Mean |     Error |    StdDev | Scaled | ScaledSD |      Gen 0 |     Gen 1 |   Allocated |
|-------------------------- |----------:|----------:|----------:|-------:|---------:|-----------:|----------:|------------:|
|  InceptionBenchmarkFarkle | 171.21 ms | 1.8336 ms | 1.7152 ms |   1.99 |     0.02 | 35000.0000 | 2000.0000 | 64435.32 KB |
| InceptionBenchmarkLazarus |  85.83 ms | 0.5621 ms | 0.5258 ms |   1.00 |     0.00 |          - |         - |     1.33 KB |
