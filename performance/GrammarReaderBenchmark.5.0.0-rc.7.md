``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.1.701
  [Host]     : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.12 (CoreCLR 4.6.27817.01, CoreFX 4.6.27818.01), 64bit RyuJIT


```
|    Method |     Mean |    Error |   StdDev |   Gen 0 |   Gen 1 | Gen 2 | Allocated |
|---------- |---------:|---------:|---------:|--------:|--------:|------:|----------:|
| Base64EGT | 532.0 us | 1.825 us | 1.524 us | 63.4766 | 20.5078 |     - | 211.56 KB |
