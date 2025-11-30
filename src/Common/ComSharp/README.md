This directory contains the precompiler's interface definition, as well as COM interop code.

Each project that wants to interoperate with COM# must directly include these files by itself. In order to enforce this, all types defined in these files have either `file` visibility, or `internal` visibility with `EmbeddedAttribute` applied.

Changes to the COM# interfaces or the specification of COM# itself, require changing the interface IDs. Other changes to the COM# interop code are always permitted.
