``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.100
  [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT


```
|       Method |     Mean |     Error |    StdDev | Ratio | RatioSD |    Gen 0 | Gen 1 | Gen 2 |  Allocated |
|------------- |---------:|----------:|----------:|------:|--------:|---------:|------:|------:|-----------:|
| FarkleCSharp | 5.108 ms | 0.0814 ms | 0.0721 ms |  1.71 |    0.06 | 890.6250 |     - |     - | 2766.63 KB |
| FarkleFSharp | 5.187 ms | 0.0601 ms | 0.0502 ms |  1.74 |    0.06 | 890.6250 |     - |     - | 2766.64 KB |
|    FsLexYacc | 2.949 ms | 0.0615 ms | 0.1010 ms |  1.00 |    0.00 |  97.6563 |     - |     - |  306.87 KB |
