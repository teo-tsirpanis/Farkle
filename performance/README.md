# Farkle's Benchmarks

This folder contains the performance benchmark results of the library.

## Notable changes

### Farkle 7.0.0

For the Farkle 7.0.0 timeframe the following have changed for the JSON benchmarks:

* Only the syntax-checking mode is benchmarked. We measure the raw parsing performance, without the overhead of allocating JSON objects.
* `Chiron` was renamed to `FParsec`.
