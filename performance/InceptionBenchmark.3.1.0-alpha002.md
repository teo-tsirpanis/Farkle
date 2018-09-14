``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.285 (1803/April2018Update/Redstone4)
Intel Core i7-7700HQ CPU 2.80GHz (Max: 2.81GHz) (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
Frequency=2742186 Hz, Resolution=364.6726 ns, Timer=TSC
.NET Core SDK=2.1.402
  [Host]     : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT


```
|                        Method |      Mean |     Error |   StdDev | Scaled | ScaledSD |      Gen 0 |    Gen 1 |    Gen 2 |   Allocated |
|------------------------------ |----------:|----------:|---------:|-------:|---------:|-----------:|---------:|---------:|------------:|
| InceptionBenchmarkFarkleEager |  35.18 ms | 0.8886 ms | 2.477 ms |   0.34 |     0.03 | 10666.6667 | 416.6667 | 166.6667 | 35843.16 KB |
|  InceptionBenchmarkFarkleLazy |  36.44 ms | 0.8151 ms | 1.269 ms |   0.35 |     0.02 | 11928.5714 | 642.8571 | 142.8571 | 39382.22 KB |
|     InceptionBenchmarkLazarus | 103.59 ms | 1.9060 ms | 3.533 ms |   1.00 |     0.00 |          - |        - |        - |      1.3 KB |
