``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.320 (1803/April2018Update/Redstone4)
Intel Core i7-7700HQ CPU 2.80GHz (Max: 2.81GHz) (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
Frequency=2742188 Hz, Resolution=364.6723 ns, Timer=TSC
.NET Core SDK=2.1.402
  [Host]     : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT


```
|                        Method |     Mean |     Error |    StdDev | Scaled | ScaledSD |     Gen 0 |    Gen 1 |    Gen 2 |  Allocated |
|------------------------------ |---------:|----------:|----------:|-------:|---------:|----------:|---------:|---------:|-----------:|
| InceptionBenchmarkFarkleEager | 29.07 ms | 0.4268 ms | 0.3992 ms |   1.29 |     0.03 | 4466.6667 | 266.6667 |  66.6667 | 16431496 B |
|  InceptionBenchmarkFarkleLazy | 33.62 ms | 0.7186 ms | 0.8554 ms |   1.49 |     0.05 | 5571.4286 | 357.1429 | 142.8571 | 20365232 B |
|     InceptionBenchmarkLazarus | 22.62 ms | 0.4498 ms | 0.4813 ms |   1.00 |     0.00 |         - |        - |        - |        0 B |
