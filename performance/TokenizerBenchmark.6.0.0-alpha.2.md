``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.423 (2004/?/20H1)
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.302
  [Host]     : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT DEBUG
  DefaultJob : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT


```
|       Method |     Mean |     Error |    StdDev | Ratio |    Gen 0 | Gen 1 | Gen 2 |  Allocated |
|------------- |---------:|----------:|----------:|------:|---------:|------:|------:|-----------:|
| FarkleCSharp | 1.744 ms | 0.0328 ms | 0.0307 ms |  0.73 | 583.9844 |     - |     - | 1793.93 KB |
| FarkleFSharp | 1.770 ms | 0.0169 ms | 0.0158 ms |  0.74 | 583.9844 |     - |     - | 1793.93 KB |
|    FsLexYacc | 2.404 ms | 0.0218 ms | 0.0193 ms |  1.00 |  93.7500 |     - |     - |  299.17 KB |
