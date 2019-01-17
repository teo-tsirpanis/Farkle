``` ini

BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17763.253 (1809/October2018Update/Redstone5)
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.2.102
  [Host]     : .NET Core 2.1.6 (CoreCLR 4.6.27019.06, CoreFX 4.6.27019.05), 64bit RyuJIT DEBUG
  DefaultJob : .NET Core 2.1.6 (CoreCLR 4.6.27019.06, CoreFX 4.6.27019.05), 64bit RyuJIT


```
|    Method |     Mean |     Error |    StdDev | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
|---------- |---------:|----------:|----------:|------------:|------------:|------------:|--------------------:|
| Base64EGT | 2.009 ms | 0.0179 ms | 0.0168 ms |    351.5625 |    128.9063 |           - |             1.52 MB |
