# Farkle 7's grammar file format

## History

Farkle started as a [GOLD Parser](http://www.goldparser.org) 5.0 engine that read [Enhanced Grammar Tables (EGT)](http://www.goldparser.org/doc/egt/index.htm) files. Each EGT file resembles a simple version of [CBOR](https://cbor.io) and is made of a _header_ (a null-terminated UTF-16 string) and a series of _records_. Each record contains _entries_, which can be empty or contain one byte, boolean, 16-bit unsigned integer, or string. On top of this structure, the standard specifies how various grammar structures are encoded.

The EGT format suffers from the following design flaws:

* __No record ordering guarantees:__ Each grammar object (symbol, group, LALR or DFA state) is specified in its own record. The problem is that the specification does not guarantee us that the objects that are referenced by index shall appear together and in order. The only guarantee is that a `TableCounts` record shall appear before the records of these objects. This complicates reading.
* __Poor mapping with Farkle's domain model:__ There are some differences in Farkle's and GOLD's view of grammars. One example is that GOLD treats terminals, nonterminals, noise symbols, group start and end symbols uniformly as "symbols", while Farkle doesn't.
* __Size inefficiencies:__ The format's choice of UTF-16 along with the use of fixed-size integers leads to wasted space. To see an example, in [an EGT file for a JSON grammar](../../tests/resources/JSON.egt) 34% of its bytes is zero.
* __Small numbers:__ EGT files can store only 16-bit integers, putting a limit to the maximum size of a grammar. A grammar with millions of terminals will not appear in real life, but 65536 feels like a too low limit.

In Farkle 6 we introduced EGTneo, a new grammar file format that Farkle uses to write its precompiled grammars. Because the structure of EGTneo files is much more strict and closely matches Farkle's domain model, the EGTneo file reader is simpler. There were also structural additions to the EGT format: new entry types for UTF-8 strings and compressed 32-bit integers allowed significant size savings, more than halving the size of [the EGTneo file for the same JSON grammar](../../tests/resources/JSON.egtn), compared to EGT.

## A new format

As we are moving to Farkle 7 there are still issues that need to be addressed:

* __Extensibility:__ EGTneo was designed very hastily and without any thoughts on how it could be extended in the future without sacrificing backwards compatibility.
* __More size reduction opportunities:__ The EGT file format which EGTneo extended is self-describing, with each entry having a leading byte indicating its type. While this is good for error detecting purposes, it unnecessarily increases size for no clear benefit, and validating errors can still be done with a specialized format.
* __Reading overhead:__ EGT and EGTneo files are intended to be read sequentially once, and deserialize the entries into objects, incurring allocations. It would be interesting to investigate whether a file format that is intended to be randomly accessed in memory will be faster to read from and allocate less objects.
* __Determinism:__ Previous formats contained the date and time the grammar was generated, which causes problems with determinism and reproducibility. The new format is an opportunity to address this mistake.

With that being said, Farkle 7 will debut with a new grammar file format.

### Goals

* Allow future additions of grammar constructs without breaking compatibility.
* Support encoding grammars that miss parsing tables and cannot be used to parse text.
* Decrease initial reading time and memory consumption, as well as file size compared to EGTneo, given the same grammar.
* Support reading GOLD Parser 5 EGT files by converting them to the new format.

### Nice-to-haves

*
    Increase data access speed for parsing.

    > Farkle uses an internal type called `OptimizedOperations` to provide faster ways to access data from the grammar. This way, the performance characteristics of using the grammar during parsing won't matter much.

### Non-goals

*
    Support round-tripping between builder objects and grammars.

    > To do that we would have to encode regexes and operator scopes in the grammars. It is planned but not immediately.
*
    Provide compatibility with the EGTneo format.

    > As soon as Farkle 7 releases, EGTneo will become a dead format and it is expected that all users of Farkle move away from older versions. There are little reasons to keep supporting EGTneo.
*
    Provide compatibility with the old grammar API.

    > There will inevitably be breaking API changes. Templates will also have to be adjusted but no third-party templates are known to exist.

### Appendix: Two decades of grammar file formats

Now that Farkle 7's development has reached a point where it can write grammars, it's a good time to look back at the various grammar file formats that GOLD Parser and Farkle have created over the years.

|Grammar format|Release date|New features|[JSON](https://github.com/teo-tsirpanis/Farkle/blob/egt5/tests/resources/JSON.grm) grammar size|[COBOL 85](https://github.com/teo-tsirpanis/Farkle/blob/egt5/tests/resources/COBOL85.grm) grammar size|
|--------------|------------|------------|-----------------|------------------|
|CGT (GOLD Parser 1.x)|2001|initial version, designed for sequential access|131004 bytes|619462 bytes|
|EGT (GOLD Parser 5.x)|2011|groups, compact character sets|4683 bytes (-96,5%)|612511 bytes (-1.1%)|
|EGTneo (Farkle 6)|2020|streamlined encoding, DFA default transitions|2304 bytes (-50.8%)|478273 bytes (-21.9%)|
|Farkle 7|2023|an entirely different format, designed for random access and future extensibility|1350 bytes (-41.4%)|305239 bytes (-36.2%)|
