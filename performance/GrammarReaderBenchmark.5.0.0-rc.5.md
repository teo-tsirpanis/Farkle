``` ini

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.14393.2457 (1607/AnniversaryUpdate/Redstone1), VM=Hyper-V
Intel Xeon CPU E5-2697 v3 2.60GHz, 1 CPU, 2 logical and 2 physical cores
.NET Core SDK=2.2.203
  [Host]     : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT


```
|    Method |     Mean |    Error |   StdDev |   Gen 0 |  Gen 1 | Gen 2 | Allocated |
|---------- |---------:|---------:|---------:|--------:|-------:|------:|----------:|
| Base64EGT | 657.7 us | 9.871 us | 7.706 us | 25.3906 | 5.8594 |     - | 170.71 KB |
