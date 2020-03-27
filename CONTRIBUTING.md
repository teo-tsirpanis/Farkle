# Contributor's guide

## How to build

* Install .NET Core SDK, version 3.1.
* `build.cmd` / `build.sh`

There are Visual Studio Code tasks to make your life easier.

## Code style

* 4 spaces indentation
* __No__ trailing spaces
* One trailing newline
* Prefer to put types in namespaces, not modules (even internal ones).

## Breaking changes policy

Minor breaking changes on minor APIs (generally anything not named or related to `DesigntimeFarkle` or `RuntimeFarkle`) are tolerable, if they do not impact the average Farkle user.
