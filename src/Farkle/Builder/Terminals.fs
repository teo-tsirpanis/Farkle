// Copyright (c) 2020 Theodore Tsirpanis
//
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

[<RequireQualifiedAccess>]
/// <summary>Some designtime Farkles that
/// are commonly used in many grammars.</summary>
/// <remarks>
/// These functions take a name and create
/// a designtime Farkle which is meant to be
/// reused everywhere it is needed in the grammar.
/// Creating and using many designtime Farkles
/// of the same or similar kind
/// will almost certainly lead to an error.</remarks>
module Farkle.Builder.Terminals

open System.ComponentModel
open Farkle.Builder
open Farkle.Builder.Regex
open System
open System.Globalization
open System.Text

/// This module is public due to compiler limitations.
/// Do not use it; it is subject to be removed or altered at any time.
// It's public because of https://github.com/dotnet/fsharp/issues/8348
[<EditorBrowsable(EditorBrowsableState.Never)>]
module Internal =
    #if !MODERN_FRAMEWORK
    type Characters = string
    let inline toCharacters (x: ReadOnlySpan<char>): Characters = x.ToString()
    #else
    type Characters = ReadOnlySpan<char>
    let inline toCharacters x: Characters = x
    #endif

open Internal

[<CompiledName("UnsignedIntRegex")>]
/// A `Regex` that recognizes a series of decimal digits.
/// Leading zeros are accepted.
// Seriously though, why not? Looking at you, JSON!
let unsignedIntRegex = Number |> chars |> atLeast 1

[<CompiledName("FSharpOnlyGenericUnsigned"); RequiresExplicitTypeArguments; NoDynamicInvocation>]
/// Creates a designtimeFarkle that parses an unsigned decimal integer
/// into the desired number type. No bounds checking is performed.
/// Using this function from a language other than F# will throw an exception.
let inline genericUnsigned< ^TInt when ^TInt: (static member Parse: Characters * NumberStyles * IFormatProvider -> ^TInt)> name =
    terminal name
        (T (fun _ x ->
            try
                (^TInt: (static member Parse: Characters * NumberStyles * IFormatProvider -> ^TInt)
                    (toCharacters x, NumberStyles.None, NumberFormatInfo.InvariantInfo))
            with
            | e -> error e.Message))
        unsignedIntRegex

[<CompiledName("SignedIntRegex")>]
/// A `Regex` that recognizes a series of decimal digits
/// that might be prefixed with a minus sign "-".
/// Leading zeros are accepted.
let signedIntRegex = (optional <| char '-') <&> unsignedIntRegex

[<CompiledName("FSharpOnlyGenericSigned"); RequiresExplicitTypeArguments; NoDynamicInvocation>]
/// Creates a designtime Farkle that parses a signed decimal integer
/// into the desired number type. No bounds checking is performed.
/// The type parameter must support the unary negation operator.
/// Using this function from a language other than F# will throw an exception.
let inline genericSigned< ^TInt when ^TInt: (static member Parse: Characters * NumberStyles * IFormatProvider -> ^TInt) and ^TInt : (static member (~-): ^TInt -> ^TInt)> name =
    terminal name
        (T (fun _ x ->
            try
                (^TInt: (static member Parse: Characters * NumberStyles * IFormatProvider -> ^TInt)
                    (toCharacters x, NumberStyles.Integer, NumberFormatInfo.InvariantInfo))
            with
            | e -> error e.Message))
        signedIntRegex

/// Creates a designtime Farkle that parses and returns a
/// signed 32-bit signed integer. No bounds checking is performed.
[<CompiledName("Int32")>]
let int name = genericSigned<int> name

[<CompiledName("Int64")>]
/// Creates a designtime Farkle that parses and returns a
/// signed 64-bit signed integer. No bounds checking is performed.
let int64 name = genericSigned<int64> name

[<CompiledName("UInt32")>]
/// Creates a designtime Farkle that parses and returns a
/// signed 32-bit unsigned integer. No bounds checking is performed.
let uint32 name = genericUnsigned<uint32> name

[<CompiledName("UInt64")>]
/// Creates a designtime Farkle that parses and returns a
/// signed 64-bit unsigned integer. No bounds checking is performed.
let uint64 name = genericUnsigned<uint64> name

[<CompiledName("UnsignedRealRegex")>]
/// A `Regex` that recognizes an unsigned real number.
/// The number is expected to be written in scientific
/// notation, with the decimal point symbol being the dot.
/// Numbers before or after the dot are optional, but they
/// must exist in at least one of the two places.
let unsignedRealRegex =
    let numberStar = chars Number |> star
    let atLeastOneNumber = chars Number <&> numberStar
    let dotOptional = char '.' |> optional
    concat [
        choice [
            // There has to be at least one digit
            // either before or after the dot.
            // Or no dot at all!
            atLeastOneNumber <&> dotOptional <&> numberStar
            numberStar <&> dotOptional <&> atLeastOneNumber
        ]
        [chars "eE"; optional <| chars "+-"; atLeastOneNumber]
        |> concat
        |> optional
    ]

[<CompiledName("SignedRealRegex")>]
/// Like `unsignedRealRegex`, but with an optional
/// sign in the beginning being allowed.
let signedRealRegex = (optional <| char '-') <&> unsignedRealRegex

[<CompiledName("FSharpOnlyGenericReal"); RequiresExplicitTypeArguments; NoDynamicInvocation>]
/// Creates a designtime Farkle that parses a real number
/// into the desired number type. No bounds checking is performed.
/// Using this function from a language other than F# will throw an exception.
let inline genericReal< ^TReal when ^TReal: (static member Parse: Characters * NumberStyles * IFormatProvider -> ^TReal)> allowSign name =
    let regex =
        if allowSign then
            signedRealRegex
        else
            unsignedRealRegex
    terminal name
        (T (fun _ x ->
            try
                (^TReal: (static member Parse: Characters * NumberStyles * IFormatProvider -> ^TReal)
                    (toCharacters x, NumberStyles.Float, NumberFormatInfo.InvariantInfo))
            with
            | e -> error e.Message))
        regex

[<CompiledName("Single")>]
/// Creates a designtime Farkle that parses and returns
/// a signed single-precision floating-point number. The number
/// is expected to be written in scientific notation, with
/// the decimal point symbol being the dot. Special values
/// such as NaN or infinity are not recognized.
let float32 name = genericReal<float32> true name

[<CompiledName("Double")>]
/// Creates a designtime Farkle that parses and returns
/// a signed double-precision floating-point number. The number
/// is expected to be written in scientific notation, with
/// the decimal point symbol being the dot. Special values
/// such as NaN or infinity are not recognized.
let float name = genericReal<float> true name

[<CompiledName("Decimal")>]
/// Creates a designtime Farkle that parses and returns
/// a signed decimal floating-point number. The number
/// is expected to be written in scientific notation, with
/// the decimal point symbol being the dot.
let decimal name = genericReal<decimal> true name

let private stringTransformer = T (fun _ span ->
    let mutable span = span.Slice(1, span.Length - 2)
    let sb = StringBuilder(span.Length)

    #if !MODERN_FRAMEWORK
    let mutable i = 0
    while i < span.Length do
        let c = span.[i]
        i <- i + 1
        match c with
        | '\\' ->
            let c = span.[i]
            i <- i + 1
            match c with
            | 'a' -> sb.Append '\a'
            | 'b' -> sb.Append '\b'
            | 'f' -> sb.Append '\f'
            | 'n' -> sb.Append '\n'
            | 'r' -> sb.Append '\r'
            | 't' -> sb.Append '\t'
            | 'u' ->
                let hexCode =
                    UInt16.Parse(span.Slice(i, 4).ToString(), NumberStyles.HexNumber)
                i <- i + 4
                sb.Append(Operators.char hexCode)
            | 'v' -> sb.Append '\v'
            | c -> sb.Append c
        | c -> sb.Append c
        |> ignore
    #else
    while not span.IsEmpty do
        match span.IndexOf('\\') with
        | -1 ->
            sb.Append(span) |> ignore
            span <- ReadOnlySpan.Empty
        | backslashIdx ->
            let charactersBeforeBackslash = span.Slice(0, backslashIdx)
            sb.Append(charactersBeforeBackslash) |> ignore
            let escapePayloadLength =
                match span.[backslashIdx + 1] with
                | 'a' -> sb.Append '\a' |> ignore; 1
                | 'b' -> sb.Append '\b' |> ignore; 1
                | 'f' -> sb.Append '\f' |> ignore; 1
                | 'n' -> sb.Append '\n' |> ignore; 1
                | 'r' -> sb.Append '\r' |> ignore; 1
                | 't' -> sb.Append '\t' |> ignore; 1
                | 'u' ->
                    let hexCode =
                        UInt16.Parse(span.Slice(backslashIdx + 2, 4), NumberStyles.HexNumber)
                    sb.Append(Operators.char hexCode) |> ignore
                    5
                | 'v' -> sb.Append '\v' |> ignore; 1
                | c -> sb.Append c |> ignore; 1
            span <- span.Slice(backslashIdx + 1 + escapePayloadLength)
    #endif

    sb.ToString())

[<CompiledName("StringEx")>]
/// <summary>Creates a designtime Farkle that can recognize a C-like
/// string. It supports escaping with a backslash "\\".</summary>
/// <param name="escapeChars">A sequence of valid escape characters after the backslash.
/// If one of <c>abfnrtv</c> is included, it will be treated as a developer would expect.
/// Any other character will be output verbatim. The backslash and the string delimiter
/// are always escaped. Specifying <c>u</c> will be ignored.</param>
/// <param name="allowEscapeUnicode">Whether to allow escaping Unicode characters with
/// <c>\\u</c>, followed by exactly four case-insensitive hex digits.</param>
/// <param name="multiline">Whether to allow line breaks in the string. They will
/// be interpreted as literal line breaks.</param>
/// <param name="delim">The character that marks the start and the end of the string.
/// In most programming languages, it is either a single or a double quote.</param>
/// <param name="name">The name of the resulting designtime Farkle.</param>
let stringEx escapeChars allowEscapeUnicode multiline delim name =
    let escapeChars =
        if allowEscapeUnicode then
            Seq.filter ((<>) 'u') escapeChars
        else
            escapeChars
    let regex =
        let stringCharacters =
            [delim; '\\'] @ if multiline then ['\r'; '\n'] else []
            |> allButChars
        concat [
            char delim
            star <| choice [
                stringCharacters
                concat [
                    char '\\'
                    choice [
                        char '\\'
                        char delim
                        chars escapeChars
                        if allowEscapeUnicode then
                            char 'u' <&> (repeat 4 <| chars "1234567890ABCDEFabcdef")
                    ]
                ]
            ]
            char delim
        ]
    terminal name stringTransformer regex

[<CompiledName("String")>]
/// Creates a designtime Farkle that can recognize a single-line, C-like string.
let string delim name = stringEx "abfnrtv" true false delim name
