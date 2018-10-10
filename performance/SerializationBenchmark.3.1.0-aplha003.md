``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.320 (1803/April2018Update/Redstone4)
Intel Core i7-7700HQ CPU 2.80GHz (Max: 2.81GHz) (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
Frequency=2742188 Hz, Resolution=364.6723 ns, Timer=TSC
.NET Core SDK=2.1.402
  [Host]     : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT


```
|    Method |     Mean |     Error |    StdDev |    Gen 0 | Allocated |
|---------- |---------:|----------:|----------:|---------:|----------:|
| Base64EGT | 1.208 ms | 0.0218 ms | 0.0204 ms | 267.5781 | 825.54 KB |
