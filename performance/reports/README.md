# Farkle's Benchmarks

This folder contains the performance benchmark results of the library.

## Notable changes

### Farkle 7.0.0

For the Farkle 7.0.0 timeframe the following have changed for the JSON benchmarks:

* Only the syntax-checking mode is benchmarked. We measure the raw parsing performance, without the overhead of allocating JSON objects.
* `Chiron` was renamed to `FParsec`.

The benchmarks were rewritten to C# and moved to the `performance` directory.

Benchmarks for version `7.0.0-pre` were performed on GitHub Actions and at commit https://github.com/teo-tsirpanis/Farkle/commit/0934f4d87306df5e9d2ed00e31c30c99e33cb1e2.

Benchmarks for version `7.0.0-pre-builder` were performed on my local machine from the previous F# benchmarks and the report was copied from https://github.com/teo-tsirpanis/Farkle/pull/263#issuecomment-2303491985.
