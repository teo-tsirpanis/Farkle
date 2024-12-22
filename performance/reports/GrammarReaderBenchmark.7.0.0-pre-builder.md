```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.4780/22H2/2022Update)
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.400
  [Host] : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2 DEBUG

Toolchain=InProcessEmitToolchain

```
| Method             | Mean         | Error      | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|------------------- |-------------:|-----------:|-----------:|-------------:|------:|--------:|---------:|---------:|----------:|------------:|
| BuildFarkle6       | 2,529.886 μs | 36.4876 μs | 34.1305 μs | 2,533.097 μs |  1.00 |    0.02 | 718.7500 | 156.2500 | 2401189 B |        1.00 |
| BuildFarkle7       |   310.304 μs |  4.5210 μs |  4.0078 μs |   310.671 μs |  0.12 |    0.00 |  81.0547 |        - |  255142 B |        0.11 |
|                    |              |            |            |              |       |         |          |          |           |             |
| LoadEGTneoFarkle6  |   140.289 μs |  1.2268 μs |  1.1476 μs |   140.130 μs |  1.00 |    0.01 |  31.2500 |        - |   98328 B |       1.000 |
| LoadGrammarFarkle7 |     6.674 μs |  0.1230 μs |  0.2485 μs |     6.544 μs |  0.05 |    0.00 |   0.1678 |        - |     528 B |       0.005 |
