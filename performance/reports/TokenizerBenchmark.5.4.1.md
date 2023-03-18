``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.200
  [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT


```
|       Method |     Mean |     Error |    StdDev | Ratio | RatioSD |    Gen 0 | Gen 1 | Gen 2 |  Allocated |
|------------- |---------:|----------:|----------:|------:|--------:|---------:|------:|------:|-----------:|
| FarkleCSharp | 3.344 ms | 0.0657 ms | 0.1098 ms |  1.25 |    0.04 | 578.1250 |     - |     - | 1801.63 KB |
| FarkleFSharp | 3.291 ms | 0.0462 ms | 0.0432 ms |  1.22 |    0.02 | 578.1250 |     - |     - | 1801.63 KB |
|    FsLexYacc | 2.709 ms | 0.0244 ms | 0.0228 ms |  1.00 |    0.00 |  97.6563 |     - |     - |  306.87 KB |
