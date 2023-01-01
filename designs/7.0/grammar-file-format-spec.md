# Farkle 7's grammar file format specification

This file describes the binary format of Farkle 7's grammars. It is heavily inspired by the Common Language Infrastructure metadata format described in [ECMA-335][ecma].

## Ground rules

* The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC2119].
* Numbers are stored in little-endian format.

## Abstract file structure

From a top-level perspective, a grammar file is made of a _header_ and a sequence of _streams_.

### Header

A grammar file starts with the following data:

|Offset|Type|Field|Description|
|------|----|-----|-----------|
|0|`uint8_t[8]`|__Magic__|Identifies the file type. Must be `46 61 72 6B 6C 65 00 00` in hexadecimal (`"Farkle\0\0"` in ASCII).|
|8|`uint16_t`|__MajorVersion__|The file's major version. Currently set to `7`. To be incremented on changes incompatible with earlier versions.|
|10|`uint16_t`|__MinorVersion__|The file's minor version. Currently set to `0`. To be incremented on changes compatible with earlier versions.|
|12|`uint32_t`|__StreamCount__|The number of stream definitions that immediately follow this header.|

* If the first eight bits of the file are different than the __Magic__ field's expected value, readers MUST NOT read past them and MUST report an error.
    > Note that the magic code ends with two zeroes. It will prevent [GOLD Parser][gold] grammar file readers from continuing reading it.
* If the value of the __MajorVersion__ field is larger than the expected one or smaller than `7`, readers MUST NOT read past the __MinorVersion__ field and MUST report an error.
    * When and if a file version after 7 gets specified, if the version of the file is smaller than the latest the reader supports, it MAY keep compatibility with the older format, or report an error.

### Stream Definition

|Offset|Type|Field|Description|
|------|----|-----|-----------|
|0|`uint8_t[8]`|__Identifier__|Identifies the kind of the stream.|
|8|`int32_t`|__Offset__|The byte offset from where the stream starts, starting from the start of this file.|
|12|`int32_t`|__Length__|The length of the stream in bytes.|

* Values of the __Identifier__ field whose first byte is equal to `23` in hexadecimal (`#` in ASCII) are reserved by implementations of the Farkle project. Third-party implementations MUST NOT define custom streams with such __Identifier__&zwnj;s.
* Each stream definition MUST be unique based on its __Identifier__ field.
* The __Offset__ and __Length__ fields MUST NOT be less than zero.

## Streams

This specification defines three stream types. After their name, the value of their corresponding __Identifier__ field in ASCII will appear in parentheses.

### String heap (`"#Strings"`)

The string heap is defined exactly as the #Strings heap in section II.24.2.3 of [ECMA-335][ecma]. A string heap MAY be absent from a grammar file; in this case all indices to the string heap in the file MUST be zero.

### Blob heap (`"#Blob\0\0\0"`)

The blob heap is defined exactly as the #Blob heap in section II.24.2.4 of [ECMA-335][ecma]. A blob heap MAY be absent from a grammar file; in this case all indices to the blob heap in the file MUST be zero.

### Table stream (`"#~\0\0\0\0\0\0"`)

The table stream begins with a header:

|Type|Field|Description|
|----|-----|-----------|
|`uint64_t`|__TablesPresent__|A bit vector of the tables present in this stream. `N` is defined as the population count of this field.|
|`int32_t[N]`|__RowCounts__|The number of rows each table has. Each value MUST be greater than zero.|
|`int8_t[N]`|__RowSizes__|The size of each table's row in bytes. Each value MUST be greater than zero.|
|`uint8_t`|__HeapSizes__|A bit vector indicating the sizes of the heaps referenced by the tables.|
|`uint8_t[(5*N + 1) mod 8]`|__Padding__|A sequence of zero to seven zeroes to make the header's size a multiple of eight bytes.|

> Because some fields have a variable size, the Offset column was omitted from the table.

> [ECMA-335][ecma] does not have a field equivalent to __RowSizes__. It was added in this specification to allow adding new tables without causing a breaking change.

The following table indicates the meaning of each bit of the __HeapSizes__ field. If a bit is set, indices to its corresponding heap have a size of two bytes. Otherwise, they have a size of four bytes.

|Bit|Description|
|---|-----------|
|0|The String heap is either absent or its size is less than or equal to 2<sup>16</sup> bytes long.|
|1|The Blob heap is either absent or its size is less than or equal to 2<sup>16</sup> bytes long.|
|2-7|Reserved. Writers MUST set them to zero and readers SHOULD ignore them.|

Immediately after the header come the tables. Each table is identified by a number from zero to sixty three corresponding to a bit in the __TablesPresent__ field, and contains one or more rows, which are structures of constant size. The tables are ordered by this number in ascending order. Using the __RowCounts__ and __RowSizes__ fields, readers can easily calculate the position and size of each table.

Each table row is stored as the concatenation of its columns. A row can be:

* An integer of fixed length.
* An index to the String or Blob heaps. Its length depends on the corresponding bit in the __HeapSizes__ field.
* An index to another table. Its length is two bytes if the table's row count is less than or equal to 2<sup>16</sup>, and four bytes otherwise.
* A coded index to one of a set of `n` possible tables. It is encoded as `e << log2(n) | tag`, where `e` is the index to the table and `tag` is a number from zero to `n - 1` that identifies the table `e` is referring to. The length of the coded index is two bytes if all possible tables have a row count less than or equal to 2<sup>16 - log2(n)</sup>, and four bytes otherwise. A table with all possible kinds of coded indices will be provided later in the specification.

The supported table types are listed in the next section.

## Tables

The specification defines the following tables, identified by the bit index of the tables stream header's __TablesPresent__ field.

|Bit|Name|
|---|----|
|0|_Grammar_|
|1|_TokenSymbol_|
|2|_Group_|
|3|_GroupNesting_|
|4|_Nonterminal_|
|5|_Production_|
|6|_ProductionMember_|
|7|_StateMachine_|
|8|_SpecialName_|
|9-58|Reserved|
|59|Permanently Held. Do Not Disturb.|
|60-63|Reserved|

### _Grammar_ table

The _Grammar_ table contains the following columns:

* __Name__ (an index to the String heap): The name of the grammar.
* __StartSymbol__ (an index to the _Nonterminal_ table): The starting nonterminal of the grammar.
* __Flags__ (a two-byte bit vector): Characteristics of the grammar. Reserved; writers MUST set it to zero and readers SHOULD ignore it.

A grammar file MUST contain a _Grammar_ table with exactly one row.

Information stored in the _Grammar_ table are for informative purposes only and intended to be used by consumers like templating systems and not parsers.

### _TokenSymbol_ table

The _TokenSymbol_ table contains the following columns:

* __Name__ (an index to the String heap): The name of the token symbol.
* __Flags__ (a four-byte bit vector): Characteristics of the token symbol.

The following bit values are defined for the __Flags__ column:

|Bit|Name|Description|
|---|----|-----------|
|0|`Terminal`|The token symbol can exist in the right-hand side of a production.|
|1|`GroupStart`|The token symbol appears in the __Start__ column of the _Group_ table.|
|2|`Noise`|The token symbol can be skipped by parsers if encountered in an unexpected place in the input.|
|3|`Hidden`|The token symbol should not be displayed by parsers in the expected tokens list in case of a syntax error.|
|4|`HasSpecialName`|The token symbol appears in the __TokenSymbol__ column of the _SpecialName_ table.|
|5|`Generated`|The token symbol was not explicitly defined by the grammar author.|

The following rules apply to the _TokenSymbol_ table:

* A token symbol with the `Terminal` flag set MUST NOT appear in the Token Symbol table after a token symbol without the `Terminal` flag set.
* A token symbol MUST NOT have both the `Terminal` and `GroupStart` flags set.

### Group table

The _Group_ table contains the following columns:

* __Name__ (an index to the String heap): The name of the group.
* __Container__ (an index to the _Group_ table): The token symbol that corresponds to this group.
* __Flags__ (a two-byte bit vector): Characteristics of the group.
* __Start__ (an index to the _TokenSymbol_ table): The token symbol that starts the group.
* __End__ (an index to the _TokenSymbol_ table): The token symbol that ends the group.
* __FirstNesting__ (an index to the _GroupNesting_ table): The index to the first row in the _GroupNesting_ table that contains the groups allowed to be nested inside this group. This group list ends before the __FirstNesting__ value of the next group, or at the end of the _GroupNesting_ table if this is the last group.

The following bit values are defined for the __Flags__ column:

|Bit|Name|Description|
|---|----|-----------|
|0|`HasNesting`|The group has a non-empty list of groups that can be nested inside it.|
|1|`EndsOnEndOfInput`|The group can also end when the end of the input is reached, without encountering the token symbol specified in the __End__ column.|
|2|`AdvanceByCharacter`|When inside this group, the parser should read the input without invoking the regular tokenizer.|
|3|`KeepEndToken`|When the group ends, the parser should keep the token that ended the group in the input stream.|

The following rules apply to the _Group_ table:

* The token symbol pointed by the __Start__ column MUST have the `GroupStart` flag set.
* The token symbols pointed by the __End__ and __Container__ columns MUST NOT have the `GroupStart` flag set.
* A group's __FirstNesting__ column MUST be greater or equal than the __FirstNesting__ column of the previous group.
* If the last group does not have any nested groups, its __FirstNesting__ column MUST be equal to the number of rows in the _GroupNesting_ table plus one.

### GroupNesting table

The _GroupNesting_ table contains the following column:

* __Group__ (an index to the _Group_ table): The group that can be nested inside another group.

[ecma]: https://www.ecma-international.org/publications-and-standards/standards/ecma-335/
[rfc2119]: https://www.rfc-editor.org/rfc/rfc2119
[gold]: http://goldparser.org
