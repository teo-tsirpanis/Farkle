``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.228 (1803/April2018Update/Redstone4)
AMD Ryzen 5 1600 Six-Core Processor (Max: 3.20GHz), 1 CPU, 12 logical and 6 physical cores
Frequency=3119137 Hz, Resolution=320.6015 ns, Timer=TSC
.NET Core SDK=2.1.302
  [Host]     : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.2 (CoreCLR 4.6.26628.05, CoreFX 4.6.26629.01), 64bit RyuJIT


```
|     Method |     Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |  Allocated |
|----------- |---------:|----------:|----------:|---------:|---------:|-----------:|
|  Base64EGT | 1.942 ms | 0.0309 ms | 0.0289 ms | 472.6563 | 164.0625 | 1781.63 KB |
| Serialized | 4.677 ms | 0.0233 ms | 0.0207 ms | 203.1250 |  85.9375 |  994.59 KB |
