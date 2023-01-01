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

The supported table types are listed in the next section.

[ecma]: https://www.ecma-international.org/publications-and-standards/standards/ecma-335/
[rfc2119]: https://www.rfc-editor.org/rfc/rfc2119
[gold]: http://goldparser.org
