# COM# source generator

This file contains a source generator for [COM#](../../designs/7.0/comsharp.md) interface wrappers. Currently it is supported only within the context of the Farkle project. It might spin off into an independent project in the future.

## Unsupported features

The source generator supports all features of the COM# specification, except for the following:

* Generating .NET callable wrappers for interfaces in shared assemblies.
