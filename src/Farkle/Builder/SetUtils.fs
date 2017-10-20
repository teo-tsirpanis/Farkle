// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.Builder

open Farkle

[<AutoOpen>]
module PredefinedSets =

    open RangeSet

    let (+) = SetEx.(+)

    let (-) = SetEx.(-)

    /// Constructs a `SetEx` that spans between two characters with the given indices.
    let inline (%..) (x1: uint16) (x2: uint16) = create (char x1) (char x2) |> Range

    /// Constructs a `SetEx` that spans between the two given items.
    let inline (@..) x1 x2 = create x1 x2 |> Range

    let private (+++) x1 x2 =
        match x1, x2 with
        | Range x1, Range x2 -> x1 ++ x2 |> Range
        | x1, x2 -> x1 + x2

    /// Constructs a `SetEx` from the characters of the given string.
    let inline chars (x: string) = x |> set |> Set

    /// Constructs a `SetEx` that contains one character.
    let inline single (x: char) = create x x |> Range

    let HT = single '\t'

    let LF = single '\n'

    let VT = single '\011'

    let FF = single '\012'

    let CR = single '\r'

    let SpaceCharacter = single ' '

    let NBSP = single '\xA0'

    let Number = 0x30us %.. 0x39us

    let Whitespace = (0x9us %.. 0xDus) +++ (0x20us %.. 0xA0us)

    let Letter = (0x41us %.. 0x5Aus) +++ (0x61us %.. 0x7Aus)

    let LetterExtended = (0xC0us %.. 0xD6us) +++ (0xD8us %.. 0xF6us) +++ (0xF8us %.. 0xFFus)

    let AlphaNumeric = Letter +++ Number

    let Printable = (0x20us %.. 0x7Eus) +++ (0xA0us %.. 0xA0us)

    let printableExtended = 0xA1us %.. 0xFFus

    let EuroSign = single '€'

    let CopyrightSign = single '©'

    let RegisteredSign = single '®'
