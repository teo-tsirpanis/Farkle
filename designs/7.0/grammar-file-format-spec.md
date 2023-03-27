# Farkle 7's grammar file format specification

This document describes the binary format of Farkle 7's grammars. It is heavily inspired by the Common Language Infrastructure metadata format described in [ECMA-335][ecma].

## Ground rules

* The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC2119].
* Numbers are stored in little-endian format.
* The total size of a grammar file MUST NOT exceed 2<sup>31</sup> - 1 bytes.

## Abstract file structure

From a top-level perspective, a grammar file is made of a _header_ and a sequence of _streams_.

### Header

A grammar file starts with the following data:

|Offset|Type|Field|Description|
|------|----|-----|-----------|
|0|`uint8_t[8]`|__Magic__|Identifies the file type. Must be `46 61 72 6B 6C 65 00 00` in hexadecimal (`"Farkle\0\0"` in ASCII).|
|8|`uint16_t`|__MajorVersion__|The file's major version. Currently set to `7`.|
|10|`uint16_t`|__MinorVersion__|The file's minor version. Currently set to `0`.|
|12|`uint32_t`|__StreamCount__|The number of stream definitions that immediately follow this header.|

* If the first eight bits of the file are different than the __Magic__ field's expected value, readers MUST NOT read past them and MUST report an error.
    > Note that the magic code ends with two zeroes. It will prevent [GOLD Parser][gold] grammar file readers from continuing reading it.
* If the value of the __MajorVersion__ field is larger than the expected one or smaller than `7`, readers MUST NOT read past the __MinorVersion__ field and MUST report an error.
    * When and if a file version after 7 gets specified, if the version of the file is smaller than the latest the reader supports, it MAY keep compatibility with the older format, or report an error.

A different than expected __MajorVersion__ field indicates that the file cannot be read at all. A different than expected __MinorVersion__ field indicates that the file can be read, but might be incorrectly interpreted.

### Stream Definition

|Offset|Type|Field|Description|
|------|----|-----|-----------|
|0|`uint8_t[8]`|__Identifier__|Identifies the kind of the stream.|
|8|`int32_t`|__Offset__|The byte offset from where the stream starts, starting from the start of this file.|
|12|`int32_t`|__Length__|The length of the stream in bytes.|

* Values of the __Identifier__ field whose first byte is equal to `0x23` (`#` in ASCII) are reserved by implementations of the Farkle project. Third-party implementations MUST NOT define custom streams with such __Identifier__&zwnj;s.
* Each stream definition MUST be unique based on its __Identifier__ field.
* The __Offset__ and __Length__ fields MUST NOT be less than zero.

## Streams

This specification defines three stream types. After their name, the value of their corresponding __Identifier__ field in ASCII will appear in parentheses.

### String heap (`"#Strings"`)

The string heap contains a sequence of zero-terminated UTF-8 strings. Indices to the heap point to the first byte of a string. The first string is always empty. A string heap MAY be absent from a grammar file; in this case all indices to the string heap in the file MUST be zero. A string heap MUST NOT contain duplicate strings. Indices to the string heap MUST NOT point to the middle of a string.

The total size of the string heap MUST NOT exceed 2<sup>29</sup> - 1 bytes.

### Blob heap (`"#Blob\0\0\0"`)

The blob heap contains a sequence of length-prefixed byte sequences (blobs). Indices to the heap point to the first byte of the blob's length. The first blob is always empty.

The length of a blob is stored as a variable-length unsigned integer (adopted from [ECMA-335][ecma]):

* If the first one byte of the blob is 0&zwnj;_bbbbbbb_<sub>2</sub>, then the rest of the 'blob' contains the _bbbbbbb_<sub>2</sub> bytes of actual data.
* If the first two bytes of the blob are 10&zwnj;_bbbbbb_<sub>2</sub> and _x_, then the rest of the 'blob' contains the (_bbbbbb_<sub>2</sub> << 8 + _x_) bytes of actual data.
* If the first four bytes of the blob are 110&zwnj;_bbbbb_<sub>2</sub>, _x_, _y_, and _z_, then the rest of the 'blob' contains the (_bbbbb_<sub>2</sub> << 24 + _x_ << 16 + _y_ << 8 + _z_) bytes of actual data.

The blob heap MAY contain unreachable data (for purposes like alignment).

The total size of the blob heap MUST NOT exceed 2<sup>29</sup> - 1 bytes.

### Table stream (`"#~\0\0\0\0\0\0"`)

The table stream begins with a header:

|Type|Field|Description|
|----|-----|-----------|
|`uint64_t`|__TablesPresent__|A bit vector of the tables present in this stream. `N` is defined as the population count of this field.|
|`int32_t[N]`|__RowCounts__|The number of rows each table has. Each value MUST be greater than zero.|
|`int8_t[N]`|__RowSizes__|The size of each table's row in bytes. Each value MUST be greater than zero.|
|`uint8_t`|__HeapSizes__|A bit vector indicating the sizes of the heaps referenced by the tables.|
|`uint8_t[(3*N + 7) mod 8]`|__Padding__|A sequence of zero to seven zeroes to make the header's size a multiple of eight bytes.|

> Because some fields have a variable size, the Offset column was omitted from the table.

> [ECMA-335][ecma] does not have a field equivalent to __RowSizes__. It was added in this specification to allow adding new tables without causing a breaking change.

The following bit values are defined for the __HeapSizes__ field. If a bit is set, indices to its corresponding heap have a size of two bytes. Otherwise, they have a size of four bytes.

|Bit|Description|
|---|-----------|
|0|The String heap is either absent or its size is less than or equal to 2<sup>16</sup> bytes long.|
|1|The Blob heap is either absent or its size is less than or equal to 2<sup>16</sup> bytes long.|

Immediately after the header come the tables. Each table is identified by a number from zero to sixty three corresponding to a bit in the __TablesPresent__ field, and contains one or more rows, which are structures of constant size. The tables are ordered by this number in ascending order. Using the __RowCounts__ and __RowSizes__ fields, readers can easily calculate the position and size of each table. After the tables, the table stream MUST NOT contain additional data.

Each table row is stored as the concatenation of its columns and indexed starting from one. A column can be:

* An integer of fixed length.
* An index to the String or Blob heaps. Its length depends on the corresponding bit in the __HeapSizes__ field.
* A compressed index to the row of another table. Its length is defined in a following section.
* A coded index to the row of one of a set of `n` possible tables. It is encoded as `e << log2(n) | tag`, where `e` is the index to the table and `tag` is a number from zero to `n - 1` that identifies the table `e` is referring to. The length of the coded index is one or two bytes if all possible tables have a row count less than 2<sup>8 - log2(n)</sup> or 2<sup>16 - log2(n)</sup> respectively, and four bytes otherwise. A table with all possible kinds of coded indices will be provided later in the specification.

A table MUST NOT have more than 2<sup>24</sup> - 1 rows, unless its specification states a lower limit.

Table and coded indices are always one-based. An index with a value of zero points to no row. Such indices MUST NOT be present in the grammar unless they are explicitly allowed.

The supported table types are listed in following sections.

## Compressed Indices

To save space, indices in Farkle grammars have a size proportional to the number of collections of the object they point to. The following table shows the maximum number of items each index size can point to:

|Number of items|Index size in bytes|
|---------------|-------------------|
|< 2<sup>8</sup> - 1|1|
|< 2<sup>16</sup> - 1|2|
|_otherwise_|4|

The same encoding is used regardless of whether the indices are one-based or zero-based (as occurs in state machines).

> The threshold before switching to a bigger index size is one less than the maximum value of the corresponding integer type. The reason for that is that some table columns can have indices that point to one more than the corresponding table's row. This way we ensure that no index overflows.

## Coded Indices

The following coded indices are defined:

### _Symbol_: 1 bit to encode tag

|Table|Tag|
|-----|---|
|_TokenSymbol_|0|
|_Nonterminal_|1|

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
* __StartSymbol__ (a compressed index to the _Nonterminal_ table): The starting nonterminal of the grammar.
* __Flags__ (a two-byte bit vector): Characteristics of the grammar.

The following bit values are defined for the __Flags__ column:

|Bit|Name|Description|
|---|----|-----------|
|0|`Unparsable`|The grammar must not be used for parsing.|
|1|`Critical`|The grammar must not be used for parsing if it contains data unknown to its reader, as defined later in this specification.|

> Note that the absence of the `Unparsable` flag does not guarantee that the grammar can be parsed. Another way for a grammar to be unparsable is if it lacks a necessary state machine. But if we have say a grammar with a production that has no members, we can emit an unusable state machine for diagnostics purposes, but set the flag.

A grammar file MUST contain a _Grammar_ table with exactly one row.

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
|4|`Generated`|The token symbol was not explicitly defined by the grammar author.|

The following rules apply to the _TokenSymbol_ table:

* The _TokenSymbol_ table MUST NOT contain more than 2<sup>20</sup> - 1 rows.
* A token symbol with the `Terminal` flag set MUST NOT appear after a token symbol without the `Terminal` flag set.
* A token symbol MUST NOT have both the `Terminal` and `GroupStart` flags set.

### Group table

The _Group_ table contains the following columns:

* __Name__ (an index to the String heap): The name of the group.
* __Container__ (a compressed index to the _TokenSymbol_ table): The token symbol that corresponds to this group.
* __Flags__ (a two-byte bit vector): Characteristics of the group.
* __Start__ (a compressed index to the _TokenSymbol_ table): The token symbol that starts the group.
* __End__ (a compressed index to the _TokenSymbol_ table): The token symbol that ends the group.
* __FirstNesting__ (a compressed index to the _GroupNesting_ table): The index to the first row in the _GroupNesting_ table that contains the groups allowed to be nested inside this group. This group list ends before the __FirstNesting__ value of the next group, or at the end of the _GroupNesting_ table if this is the last group.

The following bit values are defined for the __Flags__ column:

|Bit|Name|Description|
|---|----|-----------|
|0|`EndsOnEndOfInput`|The group can also end when the end of the input is reached, without encountering the token symbol specified in the __End__ column.|
|1|`AdvanceByCharacter`|When inside this group, the parser should read the input without invoking the regular tokenizer.|
|2|`KeepEndToken`|When the group ends, the parser should keep the token that ended the group in the input stream.|

The following rules apply to the _Group_ table:

* The token symbol pointed by the __Start__ column MUST have the `GroupStart` flag set.
* The __Start__ column MUST NOT contain duplicate values.
* The token symbols pointed by the __End__ and __Container__ columns MUST NOT have the `GroupStart` flag set.
* The value of the __FirstNesting__ column of the first group MUST be equal to one.
* If a group and all groups after it do not have any nested groups, its __FirstNesting__ column MUST be equal to the number of rows in the _GroupNesting_ table plus one.

> Before accessing the nesting of a group, readers MUST ensure that the group can actually be nested. It is possible for the __FirstNesting__ column to point to a non-existent row if no groups can be nested.

### GroupNesting table

The _GroupNesting_ table contains the following column:

* __Group__ (an index to the _Group_ table): The group that can be nested inside another group.

### Nonterminal table

The _Nonterminal_ table contains the following columns:

* __Name__ (an index to the String heap): The name of the nonterminal.
* __Flags__ (a two-byte bit vector): Characteristics of the nonterminal.
* __FirstProduction__ (an index to the _Production_ table): The index to the first production of this nonterminal. This production list ends before the __FirstProduction__ value of the next nonterminal, or at the end of the _Production_ table if this is the last nonterminal.

The following bit values are defined for the __Flags__ column:

|Bit|Name|Description|
|---|----|-----------|
|0|`Generated`|The nonterminal was not explicitly defined by the grammar author.|

The following rules apply to the _Nonterminal_ table:

* The _Nonterminal_ table MUST NOT contain more than 2<sup>20</sup> - 1 rows.
* A nonterminal's __FirstProduction__ column MUST be greater or equal than the __FirstProduction__ column of the previous nonterminal.
* The value of the __FirstProduction__ column of the first nonterminal MUST be equal to one.
* If a nonterminal and all nonterminals after it do not have any productions, its __FirstProduction__ column MUST be equal to the number of rows in the _Production_ table plus one.
> Typically nonterminals with no productions are not allowed, but the format supports encoding grammars that cannot be used for parsing.

> Before accessing the productions of a nonterminal, readers MUST ensure that the nonterminal actually has productions. Typically nonterminals with no productions are not allowed, but the format supports encoding grammars that cannot be used for parsing.

### Production table

The _Production_ table contains the following columns:

* __Head__ (an index to the _Nonterminal_ table): The nonterminal that this production belongs to.
* __FirstMember__ (an index to the _ProductionMember_ table): The index to the first member of this production. This member list ends before the __FirstMember__ value of the next production, or at the end of the _ProductionMember_ table if this is the last production.

The following rules apply to the _Production_ table:

* A production's __Head__ column MUST have a value corresponding to its head nonterminal, as determined by the __FirstProduction__ column of the _Nonterminal_ table.
* A production's __FirstMember__ column MUST be greater or equal than the __FirstMember__ column of the previous production.
* The value of the __FirstMember__ column of the first production MUST be equal to one.
* If a production and all productions after it does not have any members, its __FirstMember__ column MUST be equal to the number of rows in the _ProductionMember_ table plus one.

> Before accessing the members of a production, readers MUST ensure that it actually has members.

### ProductionMember table

The _ProductionMember_ table contains the following columns:

* __Member__ (a _Symbol_-coded index): The member of the production.

If the __Member__ column points to a token symbol, its `Terminal` flag MUST be set.

### StateMachine table

The _StateMachine_ table contains the following columns:

* __Kind__ (an eight-byte signed integer): The kind of the state machine.
* __Data__ (an index to the Blob heap): The data of the state machine.

The following values are defined for the __Kind__ column:

|Value|Description|
|-----|-----------|
|< 0|Reserved for third-party implementations.|
|0|Deterministic Finite Automaton (DFA) on 16-bit character ranges.|
|1|Deterministic Finite Automaton (DFA) on 16-bit character ranges with conflicts.|
|2|Deterministic Finite Automaton (DFA) default transitions on 16-bit character ranges.|
|3|LR(1) state machine.|
|4|Generalized LR(1) (GLR(1)) state machine.|
|_anything else_|Reserved for future use by the Farkle project.|

> Instead of GLR(1) we could have called it "LR(1) state machine with conflicts" for symmetry, but this kind of state machine has an established name. Currently there are no plans to support GLR parsing in the Farkle project.

The following rules apply to the _StateMachine_ table:

* The __Kind__ column MUST NOT contain duplicate values.
* If both state machines of __Kind__ 0 and 1, or 3 and 4 exist, they MUST describe the same state machine, with their only difference being in the preferred values in case of conflicts.
* If a state machine of __Kind__ 2 exists, a state machine of __Kind__ 0 or 1 MUST also exist.

The format of the blob pointed to by the __Data__ column depends on the value of the __Kind__ column and is specified in following sections.

### SpecialName table

The _SpecialName_ table contains the following columns:

* __Symbol__ (a _Symbol_-coded index): The symbol that has a special name.
* __Name__ (an index to the String heap): The special name of the symbol.

The following rules apply to the _SpecialName_ table:

* The __Symbol__ column MUST NOT contain duplicate values.
* The __Name__ column MUST NOT contain duplicate values.

> The use case for this table is to help custom code that integrates with parsers such as tokenizers. Since in Farkle symbols can be renamed and many can have the same name within a grammar, the special name provides a stable way to identify them (it sticks to the symbol's original name and duplicate names would cause build failures).

## State machines

### Deterministic Finite Automaton (DFA) on character ranges

A DFA's representation consists of the following data:

|Type|Field|Description|
|----|-----|-----------|
|`uint32_t`|`stateCount`|The number of states in the DFA.|
|`uint32_t`|`edgeCount`|The number of edges in the DFA.|
|`edge_t[stateCount]`|`firstEdge`|The compressed zero-based index to the first edge of each state.|
|`char_t[edgeCount]`|`rangeFrom`|The starting character of each edge's range, inclusive.|
|`char_t[edgeCount]`|`rangeTo`|The ending character of each edge's range, inclusive.|
|`state_t[edgeCount]`|`edgeTarget`|The compressed one-based index to target state of each edge, or zero if following the edge would stop the tokenizer.|
|`token_symbol_t[stateCount]`|`accept`|The compressed index to the _TokenSymbol_ table that points to the token symbol that will get accepted at each state, or zero if the state is not an accept state.|

The type `char_t` can be any unsigned integer type.

The DFA's initial state is always the first one.

A state's edges end when the next state's edges begin, or at the end of the edges if this is the last state. If a state and all states after it do not have any edges, its `firstEdge` field MUST be equal to the number of edges in the DFA plus one.

For each state, its edges' ranges MUST be disjoint and sorted in ascending order.

An edge with its `edgeTarget` field set to zero indicates that traversing this edge will stop the tokenization process. When this happens the tokenizer will return a token at the last accepting state it encountered, or fail if such state does not exist.

> The previous EGTneo format was encoding group start accept symbols by specifying their group index, to avoid traversing the group table. Here we go back to GOLD Parser's approach and encode the group's start _symbol_ instead. This decreases performance but thanks to the `GroupStart` flag we can perform the lookup only if a group is about to start.

### Deterministic Finite Automaton (DFA) on character ranges with conflicts

A DFA has conflicts when at least one of its states has more than one accept symbol. It is represented as a regular DFA with the following changes:

* A field of type `uint32_t` called `acceptCount` is added after the `edgeCount` field.
* A field of type `accept_t[stateCount]` called `firstAccept` is added before the `accept` field. It contains the compressed zero-based index to the first accept symbol of each state.
    * The type `accept_t` is the smallest of one, two or four bytes that can hold the value of the `acceptCount` field plus one.
* The `accept` field's type is changed to `token_symbol_t[acceptCount]`.

A state's accept symbols end when the next state's accept symbols begin, or at the end of the accept symbols if this is the last state. If a state and all states after it do not have any accept symbols, its `firstAccept` field MUST be equal to the number of accept symbols in the DFA plus one.

### Deterministic Finite Automaton (DFA) default transitions

To reduce the size of a DFA, we can factor out the most common transitions of a state and only specify the rest of them.

This state machine contains `stateCount` entries of type `state_t` that specify the default transition for each state. The default transition of a state will be taken if the current input character is not matched by any edge. If it is zero, the tokenization process stops.

If all states have a failing default transition, this state machine SHOULD be omitted to save space.

### LR(1) state machine

An LR(1) state machine's representation consists of the following data:

|Type|Field|Description|
|----|-----|-----------|
|`uint32_t`|`stateCount`|The number of states in the state machine.|
|`uint32_t`|`actionCount`|The number of actions in the state machine.|
|`uint32_t`|`gotoCount`|The number of GOTO actions in the state machine.|
|`action_index_t[stateCount]`|`firstAction`|The compressed zero-based index to the first action of each state.|
|`token_symbol_t[actionCount]`|`actionTerminal`|The compressed index to the _TokenSymbol_ table that specifies the terminal that triggers each action.|
|`lr_action_t[actionCount]`|`action`|The type of each action.|
|`eof_action_t[stateCount]`|`eofAction`|The type of the action to take if input ends while being on each state.|
|`goto_t[stateCount]`|`firstGoto`|The compressed zero-based index to the first GOTO action of each state.|
|`nonterminal_t[gotoCount]`|`gotoNonterminal`|The compressed index to the _Nonterminal_ table that points to the nonterminal that triggers each GOTO action.|
|`state_t[gotoCount]`|`gotoState`|The compressed zero-based index to the target state of each GOTO action.|

The type `lr_action_t` is a signed integer that describes the type of an action (shift or reduce). It is encoded as follows:

*
    An error action is encoded as `0`.
    > Typically such actions are not encoded in the grammar.
* A shift action to state `s` is encoded as `s + 1`.
*
    A reduce action to the production with the index `p` is encoded as `-p`.
    > Remember that table row indices are one-based, so the first production has an index of `1`.

Its size depends on the number of states in the state machine and the number of productions in the grammar:

|States|Productions|Size in bytes|
|------|-----------|-------------|
|≤ 2<sup>7</sup> - 2|≤ 2<sup>7</sup>|1|
|≤ 2<sup>15</sup> - 2|≤ 2<sup>15</sup>|2|
|_Otherwise_|_Otherwise_|4|

The type `eof_action_t` describes the type of the action (reduce, accept, error) to take when input ends while being on a state. It is encoded as follows and has the size of a compressed index to the Production table:

* An error action is encoded as `0`.
* An accept action is encoded as `1`.
* A reduce action to the production with the index `p` is encoded as `p + 1`.

The LR(1) state machine's initial state is always the first one.

A state's actions end when the next state's actions begin, or at the end of the actions if this is the last state. If a state and all states after it have no actions, its `firstAction` field MUST be set to the number of all actions in the state machine plus one. The same applies to GOTO actions.

For each state, its `actionTerminal` and `gotoNonterminal` fields MUST be unique and sorted in ascending order.

The token symbol pointed by the `actionTerminal` field MUST have the `Terminal` flag set.

The specific algorithm used to build this state machine (Canonical LR(1), LALR(1) or something else) is unspecified.

### GLR(1) state machine

A GLR(1) state machine has at least one state where there at least one terminal or the end of input has more than one possible action. It is represented as a regular LR(1) state machine with the following changes:

* A field of type `uint32_t` called `eofActionCount` is added after the `gotoCount` field.
* A field of type `eof_action_index_t[stateCount]` called `firstEofAction` is added before the `eofAction` field. It contains the compressed zero-based index to the first EOF action of each state.
    * The type `eof_action_index_t` is the smallest of one, two or four bytes that can hold the value of the `eofActionCount` field plus one.
* The `eofAction` field's type is changed to `eof_action_t[eofActionCount]`.
* For each state, duplicate values of the `actionTerminal` field are allowed.

## Extensibility

### Extensibility by third parties

Third parties MAY extend to the grammar file format, through the following mechanisms only:

* Streams whose first byte of the __Identifier__ field is not `0x23`.
* State machines whose __Kind__ is a negative number.

Third parties MUST NOT diverge from this specification in any other way (including but not limited to custom tables or flag values).

### Unknown data

Readers SHOULD expose an API that indicates whether a grammar file contains data unknown to them. Its value MUST be true if the grammar file has one of the following and false otherwise:

* A __MinorVersion__ field that is greater than the latest __MinorVersion__ field the reader knows.
* A stream whose __Identifier__ field is not known to the reader.
* A state machine whose __Kind__ is not known to the reader.
* A table whose kind is not specified in this specification.

[ecma]: https://www.ecma-international.org/publications-and-standards/standards/ecma-335/
[rfc2119]: https://www.rfc-editor.org/rfc/rfc2119
[gold]: http://goldparser.org
