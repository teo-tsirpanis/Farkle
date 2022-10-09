# Contributor's guide

## How to build

* Install .NET 6 SDK
* Install PowerShell
* `build.ps1`

There are Visual Studio Code tasks to make your life easier.

## Code style

* 4 spaces indentation
* No trailing whitespace
* One trailing newline
* Prefer to put types in namespaces, not modules (even internal ones).

## What to contribute

__Farkle is undergoing a complete rewrite. Until it completes, community contributions to files under `src/Farkle` will not be accepted.__ You can contribute instead to:

* Documentation
* Tests
* Samples
* The CLI tool

## Breaking changes policy

Minor breaking changes on minor APIs (generally anything not named or related to `DesigntimeFarkle` or `RuntimeFarkle`) are tolerable, if they do not impact the average Farkle user.
