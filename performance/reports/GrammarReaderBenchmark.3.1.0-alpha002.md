``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.285 (1803/April2018Update/Redstone4)
Intel Core i7-7700HQ CPU 2.80GHz (Max: 2.81GHz) (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
Frequency=2742186 Hz, Resolution=364.6726 ns, Timer=TSC
.NET Core SDK=2.1.402
  [Host]     : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT


```
|     Method |       Mean |    Error |   StdDev |    Gen 0 |   Gen 1 | Allocated |
|----------- |-----------:|---------:|---------:|---------:|--------:|----------:|
|  Base64EGT |   972.4 us | 18.67 us | 23.62 us | 273.4375 |       - | 843.23 KB |
| Serialized | 3,149.7 us | 62.00 us | 71.39 us | 195.3125 | 74.2188 | 994.59 KB |
