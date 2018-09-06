``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.228 (1803/April2018Update/Redstone4)
AMD Ryzen 5 1600 Six-Core Processor (Max: 3.20GHz), 1 CPU, 12 logical and 6 physical cores
Frequency=3119137 Hz, Resolution=320.6015 ns, Timer=TSC
.NET Core SDK=2.1.302
  [Host]     : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT


```
|                        Method |     Mean |     Error |    StdDev | Scaled | ScaledSD |      Gen 0 |     Gen 1 |    Gen 2 |   Allocated |
|------------------------------ |---------:|----------:|----------:|-------:|---------:|-----------:|----------:|---------:|------------:|
| InceptionBenchmarkFarkleEager | 51.51 ms | 0.3498 ms | 0.3272 ms |   1.02 |     0.02 | 39000.0000 | 1000.0000 | 400.0000 | 45629.11 KB |
|  InceptionBenchmarkFarkleLazy | 55.02 ms | 0.4686 ms | 0.4154 ms |   1.09 |     0.02 | 44222.2222 | 2111.1111 | 444.4444 | 49168.17 KB |
|     InceptionBenchmarkLazarus | 50.30 ms | 0.8394 ms | 0.7441 ms |   1.00 |     0.00 |          - |         - |        - |      1.3 KB |
