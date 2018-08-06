// Copyright (c) 2017 Theodore Tsirpanis
// 
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

namespace Farkle.EGTFile

open Farkle
open Farkle.Monads

module internal EGTReader =

    open StateResult
    open System.Text

    module private Implementation =
    
        let takeByte: StateResult<byte, _, _> = List.takeOne() |> mapFailure ListError

        let takeBytes count: StateResult<byte list, _, _> = count |> List.takeM |> mapFailure ListError

        let ensureLittleEndian x =
            if System.BitConverter.IsLittleEndian then
                x
            else
                ((x &&& 0xffus) <<< 8) ||| ((x >>> 8) &&& 0xffus)

        let takeUInt16 = sresult {
            let! bytes = takeBytes 2 <!> Array.ofList
            return System.BitConverter.ToUInt16(bytes, 0) |> ensureLittleEndian
        }

        let takeString = sresult {
            let! len =
                get
                <!> Seq.pairs
                <!> Seq.tryFindIndex (fun (x, y) -> x = 0uy && y = 0uy)
                <!> failIfNone UnterminatedString
                >>= liftResult
                <!> ((*) 2)
            let! result = takeBytes len <!> Array.ofSeq <!> Encoding.Unicode.GetString
            let! terminator = takeUInt16
            if terminator = 0us then
                return result
            else
                return! fail TakeStringBug
        }

        let readEntry = sresult {
            let! entryCode = takeByte
            match entryCode with
            | 'E'B -> return Empty
            | 'b'B -> return! takeByte <!> Byte
            | 'B'B ->
                let! value = takeByte
                match value with
                | 0uy -> return Boolean false
                | 1uy -> return Boolean true
                | x -> return! x |> InvalidBoolValue |> fail
            | 'I'B -> return! takeUInt16 <!> UInt16
            | 'S'B -> return! takeString <!> String
            | x -> return! x |> InvalidEntryCode |> fail
        }

        let readRecord = sresult {
            let! tag = takeByte
            if tag <> 'M'B then
                do! tag |> InvalidRecordTag |> fail
            let! count = takeUInt16 <!> int
            return! count |> repeatM readEntry <!> List.ofSeq <!> Record
        }

    open Implementation

    let readEGT x =
        let impl = sresult {
            let! header = takeString
            let! records = whileFull readRecord <!> List.ofSeq
            return {Header = header; Records = records}
        }
        x |> List.ofSeq |> eval impl

    let eitherEntry fEmpty fByte fBoolean fUInt16 fString = sresult {
        let! entry = List.takeOne() |> mapFailure ListError
        return!
            match entry with
            | Empty -> fEmpty ()
            | Byte x -> fByte x
            | Boolean x -> fBoolean x
            | UInt16 x -> fUInt16 x
            | String x -> fString x
    }

    let wantEmpty, wantByte, wantBoolean, wantUInt16, wantString =
        let fail x = fun _ -> x |> InvalidEntryType |> StateResult.fail
        let failEmpty x = fail "Empty" x
        let failByte x = fail "Byte" x
        let failBoolean x = fail "Boolean" x
        let failUInt16 x = fail "UInt16" x
        let failString x = fail "String" x
        let ok x = returnM x
        let wantEmpty  = eitherEntry ok failByte failBoolean failUInt16 failString
        let wantByte = eitherEntry failEmpty ok failBoolean failUInt16 failString
        let wantBoolean = eitherEntry failEmpty failByte ok failUInt16 failString
        let wantUInt16 = eitherEntry failEmpty failByte failBoolean ok failString
        let wantString = eitherEntry failEmpty failByte failBoolean failUInt16 ok
        wantEmpty, wantByte, wantBoolean, wantUInt16, wantString