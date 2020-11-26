# Farkle's roadmap

This document outlines the features that are planned for Farkle's future releases.

Each feature is grouped by release, and is assigned an estimated difficulty and time to completion.

### Legend:

|Symbol|Meaning|Cost|
|------|-------|----|
|🔰|Easy|1 day|
|🔺|Easy-to-Medium|3 days|
|💠|Medium|1 week|
|✴️|Medium-to-Hard|2 weeks|
|✳️|Hard|4 weeks|
|❇️|Hard-to-Tough|2 months|
|⚜️|Tough|More than 2 months|

## Farkle 6.0.0 (January 2021)

* [Documentation generator](https://github.com/teo-tsirpanis/Farkle/issues/12) ✴️
* [Operator precedence and associativity](https://github.com/teo-tsirpanis/Farkle/issues/10) 💠
    * Needs documentation; the calculator sample and the quick start guide will have to be rewritten
* Virtual Terminals 🔺
    * An example of using them (together with the `CharStream` API) needs to be written
* Better dynamically generated post-processors 🔺
* A .NET Framework-based precompiler 💠
    * Trying it again will be a stretch goal for this release.
    * `Farkle.Tools.MSBuild.Tests` will have to build on both editions of MSBuild.

## Farkle's Endgame

Farkle's Endgame (tentative title) will be a new project based on Farkle. More details will be announced in the future.

### Working Prototype (January 2021 - June 2021) ⚜️

### Main Development (June 2021 - June 2022) ⚜️

## Mid-term ideas

* `CharStream` overhaul (again) ✳️
    * The new API will support:
        * Reentrancy
        * Parsing `ReadOnlySpan`s of characters
        * Parsing `TextReader`s asynchronously
    * There are lots of lingering API questions
    * It's going to be breaking for those who directly use `CharStream`s

## Long-term ideas

* Further optimization of the DFA generator ✳️
    * Algorithmically challenging
* Direct UTF-8 support ❇️
    * Farkle's main unit of text is the UTF-16 `char`; it will become `byte`
    * A __very big__ change; it must be evaluated whether it's worthwhile
* Support for creating parser tables using the [IELR algorithm](https://www.sciencedirect.com/science/article/pii/S0167642309001191) ❇️
    * Will eliminate potential unexplained LALR conflicts, increasing usability
    * The LALR builder APIs will need refactoring
    * Algorithmically challenging; needs research; never before has it been done on a known .NET project
* Expanded dynamic code generation ❇️
    * Use dynamic code in the tokenizer and the parser
    * Statically generate code to the assembly at precompilation
