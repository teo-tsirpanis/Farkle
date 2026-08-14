# Contributor's guide

## How to build

Open the solution and build it, or run `build.ps1`. There are VSCode tasks for some of the build script targets.

## Code style

* 4 spaces indentation
* No trailing whitespace
* One trailing newline

## What to contribute

Contributions are appreciated to the following areas:

* Bug fixes
* Performance improvements
* New features (please open an issue first)
* Documentation
* Tests
* Samples
* The CLI tool (`src/Farkle.Tools`)
* [Localizing](#localization) Farkle to a language you know

### Localization

Farkle's diagnostic messages can be localized. The officially supported languages are English and Greek. If you know any other language and want to translate them, it would be great!

To do so you need to find the `.resx` files in the repository, and create a new one corresponding to your language's culture name.
