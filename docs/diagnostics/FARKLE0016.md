# FARKLE0016: Incompatible precompiler interface

This error is emitted by the precompiler when the versions of the `Farkle` and `Farkle.Tools.MSBuild` packages are incompatible with each other. Since Farkle 7, the precompiler's implementation is split between these two packages, and they use a special internal interface to interact. While this allows some degree of version flexibility between the two packages, this interface may change in incompatible ways at any time.

To fix this error, ensure that the `Farkle` and `Farkle.Tools.MSBuild` package dependencies used in your project are updated.

> [!NOTE]
> In general, you are recommended to use the same version for both packages whenever possible.
