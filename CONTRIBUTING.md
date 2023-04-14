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

As the main Farkle library is undergoing a complete rewrite, contributions will be appreciated to the following areas:

* Documentation
* Tests
* Samples
* The CLI tool (`src/Farkle.Tools`)
* [Localizing](#localization) Farkle to a language you know

### Localization

Farkle's diagnostic messages can be localized. The officially supported languages are English and Greek. If you know any other language and want to translate them, it would be great!

To do so you need to find the `.resx` files in the repository, and create a new one corresponding to your language's culture name.

## Breaking changes policy

Minor breaking changes on minor APIs (generally anything not named or related to `DesigntimeFarkle` or `RuntimeFarkle`) are tolerable, if they do not impact the average Farkle user.
