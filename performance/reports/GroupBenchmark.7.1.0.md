```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method | InputSize | EmitGroupOptimizedDfa | Mean        | Error    | StdDev   | Allocated |
|------- |---------- |---------------------- |------------:|---------:|---------:|----------:|
| **Parse**  | **10**        | **False**                 |    **218.8 ns** |  **0.19 ns** |  **0.16 ns** |         **-** |
| **Parse**  | **10**        | **True**                  |    **175.2 ns** |  **1.16 ns** |  **1.08 ns** |         **-** |
| **Parse**  | **10000**     | **False**                 | **75,339.6 ns** | **61.82 ns** | **51.62 ns** |         **-** |
| **Parse**  | **10000**     | **True**                  | **34,154.8 ns** | **19.90 ns** | **17.64 ns** |         **-** |
